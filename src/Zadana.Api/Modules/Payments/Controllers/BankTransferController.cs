using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zadana.Api.Controllers;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Finances.Services;
using Zadana.Domain.Modules.Finances.Enums;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Finance;

namespace Zadana.Api.Modules.Payments.Controllers;

/// <summary>
/// Bank transfer payment flow: customer uploads proof, admin confirms or rejects.
/// Per section 11 of the revised SAR-only workflow.
/// </summary>
[Route("api")]
[Tags("Payments - Bank Transfer")]
public class BankTransferController(
    IApplicationDbContext context,
    FinancialEventPostingService postingService,
    WalletProjectionUpdater walletProjectionUpdater) : ApiControllerBase
{
    /// <summary>
    /// Customer uploads bank transfer proof for a pending order. The order id
    /// itself is the auth token at this stage; we tighten this once the
    /// customer mobile app sends a JWT.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("orders/{orderId:guid}/bank-transfer-proof")]
    public async Task<IActionResult> UploadProof(
        Guid orderId,
        [FromBody] BankTransferProofRequest request,
        CancellationToken cancellationToken)
    {
        var order = await context.Orders
            .FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken)
            ?? throw new NotFoundException("Order", orderId);

        if (order.PaymentMethod != PaymentMethodType.BankTransfer)
        {
            throw new BusinessRuleException("INVALID_PAYMENT_METHOD", "This order is not a bank transfer order.");
        }

        if (order.Status != OrderStatus.PendingBankConfirmation && order.Status != OrderStatus.PendingPayment)
        {
            throw new BusinessRuleException("ORDER_NOT_PENDING_BANK", "Order is not awaiting bank transfer confirmation.");
        }

        // Move to PendingBankConfirmation if still in PendingPayment
        if (order.Status == OrderStatus.PendingPayment)
        {
            order.ChangeStatus(OrderStatus.PendingBankConfirmation, null, "Bank transfer proof uploaded");
        }

        // Store proof metadata on the payment record
        var payment = await context.Payments
            .FirstOrDefaultAsync(x => x.OrderId == orderId && x.Method == PaymentMethodType.BankTransfer, cancellationToken)
            ?? throw new NotFoundException("Payment", orderId);

        payment.ApplyProviderFetch(
            providerStatus: "proof_uploaded",
            providerReferenceNumber: request.BankReference,
            rawFetchResponse: System.Text.Json.JsonSerializer.Serialize(new
            {
                request.SenderName,
                request.BankReference,
                request.TransferDate,
                request.Amount,
                request.ReceiptFileUrl,
                uploadedAt = DateTime.UtcNow,
            }));

        await context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Bank transfer proof uploaded successfully. Awaiting admin confirmation.",
            orderId = order.Id,
            orderStatus = order.Status.ToString(),
        });
    }

    /// <summary>
    /// Admin confirms a bank transfer payment. Posts ledger entry:
    /// Dr PlatformCash / Cr CustomerAdvance, then moves order to PendingVendorAcceptance.
    /// </summary>
    [HttpPost("admin/payments/{paymentId:guid}/confirm-bank-transfer")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> ConfirmBankTransfer(
        Guid paymentId,
        [FromBody] ConfirmBankTransferRequest? request,
        CancellationToken cancellationToken)
    {
        var payment = await context.Payments
            .Include(x => x.Order)
            .FirstOrDefaultAsync(x => x.Id == paymentId, cancellationToken)
            ?? throw new NotFoundException("Payment", paymentId);

        if (payment.Method != PaymentMethodType.BankTransfer)
        {
            throw new BusinessRuleException("INVALID_PAYMENT_METHOD", "This payment is not a bank transfer.");
        }

        if (payment.Status == PaymentStatus.Paid)
        {
            return Ok(new { message = "Already confirmed.", paymentId, orderStatus = payment.Order.Status.ToString() });
        }

        if (payment.Status is not (PaymentStatus.Initiated or PaymentStatus.Pending))
        {
            throw new BusinessRuleException("PAYMENT_NOT_CONFIRMABLE", $"Payment in status {payment.Status} cannot be confirmed.");
        }

        var order = payment.Order;
        CurrencyPolicy.EnsureOfficial(order.Currency);

        // Validate amount if provided
        if (request?.Amount is > 0 && request.Amount != order.TotalAmount)
        {
            throw new BusinessRuleException("BANK_TRANSFER_AMOUNT_MISMATCH",
                $"Confirmed amount {request.Amount} does not match order total {order.TotalAmount}.");
        }

        // Mark payment as paid
        payment.MarkAsPaid(request?.BankReference);

        // Move order to PendingVendorAcceptance
        if (order.Status is OrderStatus.PendingBankConfirmation or OrderStatus.PendingPayment)
        {
            order.ChangeStatus(OrderStatus.PendingVendorAcceptance, null, "Bank transfer confirmed by admin");
        }

        await context.SaveChangesAsync(cancellationToken);

        // Post ledger: Dr PlatformCash / Cr CustomerAdvance
        var idempotencyKey = $"bank-transfer-confirmed:{paymentId:N}";
        var posting = await postingService.PostAsync(
            FinancialEventType.BankTransferConfirmed,
            idempotencyKey,
            [
                new JournalLineDraft(
                    FinancialAccountCode.PlatformCash,
                    order.TotalAmount,
                    0m,
                    FinancialOwnerType.Platform,
                    Guid.Parse("00000000-0000-0000-0000-000000000001"),
                    order.Id,
                    Memo: $"Bank transfer confirmed for order {order.OrderNumber}"),
                new JournalLineDraft(
                    FinancialAccountCode.CustomerAdvance,
                    0m,
                    order.TotalAmount,
                    FinancialOwnerType.Customer,
                    order.UserId,
                    order.Id,
                    Memo: $"Customer advance on bank transfer for order {order.OrderNumber}"),
            ],
            orderId: order.Id,
            currencyCode: CurrencyPolicy.OfficialCurrency,
            description: $"Bank transfer confirmed for order {order.OrderNumber}",
            cancellationToken: cancellationToken);

        if (!posting.WasAlreadyPosted)
        {
            await walletProjectionUpdater.ApplyJournalEntryAsync(posting.JournalEntryId, cancellationToken);
        }

        return Ok(new
        {
            message = "Bank transfer confirmed. Order moved to vendor acceptance.",
            paymentId,
            orderStatus = order.Status.ToString(),
            journalEntryId = posting.JournalEntryId,
        });
    }

    /// <summary>
    /// Admin rejects a bank transfer payment. Marks payment as failed and
    /// optionally cancels the order.
    /// </summary>
    [HttpPost("admin/payments/{paymentId:guid}/reject-bank-transfer")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> RejectBankTransfer(
        Guid paymentId,
        [FromBody] RejectBankTransferRequest? request,
        CancellationToken cancellationToken)
    {
        var payment = await context.Payments
            .Include(x => x.Order)
            .FirstOrDefaultAsync(x => x.Id == paymentId, cancellationToken)
            ?? throw new NotFoundException("Payment", paymentId);

        if (payment.Method != PaymentMethodType.BankTransfer)
        {
            throw new BusinessRuleException("INVALID_PAYMENT_METHOD", "This payment is not a bank transfer.");
        }

        if (payment.Status == PaymentStatus.Paid)
        {
            throw new BusinessRuleException("PAYMENT_ALREADY_CONFIRMED", "Cannot reject an already confirmed payment.");
        }

        payment.MarkAsFailed(request?.Reason ?? "Bank transfer rejected by admin.");

        var order = payment.Order;
        if (order.Status is OrderStatus.PendingBankConfirmation or OrderStatus.PendingPayment)
        {
            order.ChangeStatus(OrderStatus.Cancelled, null, request?.Reason ?? "Bank transfer rejected");
        }

        await context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Bank transfer rejected.",
            paymentId,
            orderStatus = order.Status.ToString(),
        });
    }
}

public record BankTransferProofRequest(
    string? ReceiptFileUrl,
    string? BankReference,
    string? SenderName,
    string? TransferDate,
    decimal? Amount);

public record ConfirmBankTransferRequest(
    string? BankReference,
    decimal? Amount);

public record RejectBankTransferRequest(
    string? Reason);
