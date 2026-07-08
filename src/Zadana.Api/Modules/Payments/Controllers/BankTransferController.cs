using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using MediatR;
using Zadana.Api.Controllers;
using Zadana.Api.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Settings;
using Zadana.Application.Modules.EmailCenter;
using Zadana.Application.Modules.EmailCenter.DTOs;
using Zadana.Application.Modules.EmailCenter.Interfaces;
using Zadana.Application.Modules.Finances.Services;
using Zadana.Application.Modules.Orders.Events;
using Zadana.Application.Modules.Orders.Support;
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
    WalletProjectionUpdater walletProjectionUpdater,
    IPublisher publisher,
    IEmailCenterService emailCenterService,
    IOptions<BankTransferSettingsOptions> settings,
    IOptions<FinancialSettingsOptions> financialSettings,
    IWebHostEnvironment environment,
    ILogger<BankTransferController> logger) : ApiControllerBase
{
    private const string WebhookSecretHeader = "X-BankTransfer-Secret";

    /// <summary>
    /// Customer uploads bank transfer proof for a pending order.
    /// Authenticated customers must own the order; unauthenticated callers are
    /// rejected. This closes the IDOR risk where any caller knowing an order
    /// id could mutate the order's payment record.
    /// </summary>
    [Authorize(Policy = "CustomerOnly")]
    [HttpPost("orders/{orderId:guid}/bank-transfer-proof")]
    public async Task<IActionResult> UploadProof(
        Guid orderId,
        [FromBody] BankTransferProofRequest request,
        [FromServices] ICurrentUserService currentUserService,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId
            ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");

        var order = await context.Orders
            .FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken)
            ?? throw new NotFoundException("Order", orderId);

        if (order.UserId != userId)
        {
            // Do not reveal whether the order exists or belongs to someone else.
            throw new NotFoundException("Order", orderId);
        }

        if (order.PaymentMethod != PaymentMethodType.BankTransfer)
        {
            throw new BusinessRuleException("INVALID_PAYMENT_METHOD", "This order is not a bank transfer order.");
        }

        if (order.Status != OrderStatus.PendingBankConfirmation && order.Status != OrderStatus.PendingPayment)
        {
            throw new BusinessRuleException("ORDER_NOT_PENDING_BANK", "Order is not awaiting bank transfer confirmation.");
        }

        OrderStatus? oldStatus = null;

        // Move to PendingBankConfirmation if still in PendingPayment
        if (order.Status == OrderStatus.PendingPayment)
        {
            oldStatus = order.Status;
            order.ChangeStatus(OrderStatus.PendingBankConfirmation, null, "Bank transfer proof uploaded");
            OrderStatusHistoryTracking.TrackNewEntries(context, order);
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

        if (oldStatus.HasValue)
        {
            await PublishOrderStatusChangedAsync(
                order,
                oldStatus.Value,
                order.Status,
                notifyVendor: false,
                actorRole: "customer",
                cancellationToken);
        }

        return Ok(new
        {
            message = ApiLocalizedMessages.Resolve(HttpContext, "BANK_TRANSFER_PROOF_UPLOADED"),
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

        var result = await ConfirmBankPaymentAsync(
            payment,
            request?.BankReference,
            request?.Amount,
            "admin",
            cancellationToken);

        return Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("payments/bank-transfer/webhook")]
    public async Task<IActionResult> ReceiveBankTransferWebhook(
        [FromBody] BankTransferWebhookRequest request,
        CancellationToken cancellationToken)
    {
        ValidateWebhookSecret();

        var reference = FirstNonEmpty(request.Reference, request.BankReference, request.PaymentReference);
        if (string.IsNullOrWhiteSpace(reference))
        {
            throw new BusinessRuleException("BANK_TRANSFER_REFERENCE_REQUIRED", "Bank transfer webhook must include a payment reference.");
        }

        if (!IsPaidWebhookStatus(request.Status))
        {
            return Ok(new
            {
                processed = false,
                status = "ignored",
                message = ApiLocalizedMessages.Resolve(HttpContext, "BANK_TRANSFER_WEBHOOK_STATUS_IGNORED"),
            });
        }

        var payment = await context.Payments
            .Include(x => x.Order)
            .FirstOrDefaultAsync(
                x => x.Method == PaymentMethodType.BankTransfer &&
                     x.ProviderTransactionId == reference.Trim(),
                cancellationToken)
            ?? throw new NotFoundException("Payment", reference);

        var result = await ConfirmBankPaymentAsync(
            payment,
            FirstNonEmpty(request.TransactionId, request.BankReference, request.Reference),
            request.Amount,
            "bank_transfer_webhook",
            cancellationToken);

        return Ok(new
        {
            processed = true,
            result.PaymentId,
            result.OrderId,
            result.OrderStatus,
            result.PaymentStatus,
            result.JournalEntryId,
            result.Message,
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

        var rejectionReason = request?.Reason ?? "Bank transfer rejected by admin.";
        payment.MarkAsFailed(rejectionReason);

        var order = payment.Order;
        OrderStatus? oldStatus = null;
        if (order.Status is OrderStatus.PendingBankConfirmation or OrderStatus.PendingPayment)
        {
            oldStatus = order.Status;
            order.ChangeStatus(OrderStatus.Cancelled, null, rejectionReason);
            OrderStatusHistoryTracking.TrackNewEntries(context, order);
        }

        await context.SaveChangesAsync(cancellationToken);
        await DispatchBankTransferRejectedEmailAsync(order.Id, rejectionReason, cancellationToken);

        if (oldStatus.HasValue)
        {
            await PublishOrderStatusChangedAsync(
                order,
                oldStatus.Value,
                order.Status,
                notifyVendor: false,
                actorRole: "admin",
                cancellationToken);
        }

        return Ok(new
        {
            message = ApiLocalizedMessages.Resolve(HttpContext, "BANK_TRANSFER_REJECTED_SUCCESS"),
            paymentId,
            orderStatus = order.Status.ToString(),
        });
    }

    private async Task DispatchBankTransferRejectedEmailAsync(
        Guid orderId,
        string reason,
        CancellationToken cancellationToken)
    {
        try
        {
            var emailData = await context.Orders
                .AsNoTracking()
                .Where(item => item.Id == orderId)
                .Select(item => new
                {
                    item.Id,
                    item.OrderNumber,
                    item.UserId,
                    item.VendorId,
                    CustomerName = item.User.FullName,
                    CustomerEmail = item.User.Email,
                    VendorName = string.IsNullOrWhiteSpace(item.Vendor.BusinessNameEn)
                        ? item.Vendor.BusinessNameAr
                        : item.Vendor.BusinessNameEn
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (emailData is null)
            {
                return;
            }

            await emailCenterService.DispatchSystemEventEmailAsync(
                new EmailSystemEventDispatchRequest(
                    EventKey: EmailEventKeys.CustomerOrderImportantUpdate,
                    AudienceType: "customers",
                    To: string.IsNullOrWhiteSpace(emailData.CustomerEmail) ? [] : [emailData.CustomerEmail],
                    Variables: new Dictionary<string, string>
                    {
                        ["customer_name"] = string.IsNullOrWhiteSpace(emailData.CustomerName) ? "Customer" : emailData.CustomerName,
                        ["order_number"] = emailData.OrderNumber,
                        ["vendor_name"] = emailData.VendorName,
                        ["update_message"] = $"Bank transfer was rejected for order {emailData.OrderNumber}: {reason}"
                    },
                    TargetUrl: $"/orders/{emailData.Id}",
                    EntityId: emailData.Id,
                    RecipientEntityId: emailData.UserId,
                    VendorId: emailData.VendorId),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to dispatch bank-transfer rejection email for order {OrderId}.", orderId);
        }
    }

    private async Task<BankTransferConfirmationResult> ConfirmBankPaymentAsync(
        Zadana.Domain.Modules.Payments.Entities.Payment payment,
        string? bankReference,
        decimal? amount,
        string actorRole,
        CancellationToken cancellationToken)
    {
        if (payment.Method != PaymentMethodType.BankTransfer)
        {
            throw new BusinessRuleException("INVALID_PAYMENT_METHOD", "This payment is not a bank transfer.");
        }

        if (payment.Status == PaymentStatus.Paid)
        {
            var healedJournalEntryId = await TryPostBankTransferLedgerAsync(
                payment,
                payment.Order,
                cancellationToken);

            return new BankTransferConfirmationResult(
                "Already confirmed.",
                payment.Id,
                payment.Order.Id,
                payment.Order.Status.ToString(),
                payment.Status.ToString(),
                healedJournalEntryId);
        }

        if (payment.Status is not (PaymentStatus.Initiated or PaymentStatus.Pending))
        {
            throw new BusinessRuleException("PAYMENT_NOT_CONFIRMABLE", $"Payment in status {payment.Status} cannot be confirmed.");
        }

        var order = payment.Order;
        CurrencyPolicy.EnsureOfficial(order.Currency);

        if (amount is > 0 && amount != order.TotalAmount)
        {
            throw new BusinessRuleException(
                "BANK_TRANSFER_AMOUNT_MISMATCH",
                $"Confirmed amount {amount} does not match order total {order.TotalAmount}.");
        }

        var oldStatus = order.Status;
        payment.MarkAsPaid(bankReference);

        if (order.Status is OrderStatus.PendingBankConfirmation or OrderStatus.PendingPayment)
        {
            order.ChangeStatus(OrderStatus.PendingVendorAcceptance, null, $"Bank transfer confirmed by {actorRole}");
            OrderStatusHistoryTracking.TrackNewEntries(context, order);
        }

        await context.SaveChangesAsync(cancellationToken);

        var journalEntryId = await TryPostBankTransferLedgerAsync(payment, order, cancellationToken);

        if (oldStatus != order.Status)
        {
            try
            {
                await publisher.Publish(
                    new OrderStatusChangedNotification(
                        order.Id,
                        order.UserId,
                        order.VendorId,
                        order.OrderNumber,
                        oldStatus,
                        order.Status,
                        NotifyCustomer: true,
                        NotifyVendor: true,
                        ActorRole: actorRole),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "[BankTransfer] Status notification failed after confirming order {OrderId} payment {PaymentId}.",
                    order.Id,
                    payment.Id);
            }
        }

        return new BankTransferConfirmationResult(
            "Bank transfer confirmed. Order moved to vendor acceptance.",
            payment.Id,
            order.Id,
            order.Status.ToString(),
            payment.Status.ToString(),
            journalEntryId);
    }

    private async Task PublishOrderStatusChangedAsync(
        Zadana.Domain.Modules.Orders.Entities.Order order,
        OrderStatus oldStatus,
        OrderStatus newStatus,
        bool notifyVendor,
        string actorRole,
        CancellationToken cancellationToken)
    {
        try
        {
            await publisher.Publish(
                new OrderStatusChangedNotification(
                    order.Id,
                    order.UserId,
                    order.VendorId,
                    order.OrderNumber,
                    oldStatus,
                    newStatus,
                    NotifyCustomer: true,
                    NotifyVendor: notifyVendor,
                    ActorRole: actorRole),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "[BankTransfer] Status notification failed for order {OrderId} transition {OldStatus}->{NewStatus}.",
                order.Id,
                oldStatus,
                newStatus);
        }
    }

    private async Task<Guid?> TryPostBankTransferLedgerAsync(
        Zadana.Domain.Modules.Payments.Entities.Payment payment,
        Zadana.Domain.Modules.Orders.Entities.Order order,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = $"bank-transfer-confirmed:{payment.Id:N}";
        try
        {
            var posting = await postingService.PostAsync(
                FinancialEventType.BankTransferConfirmed,
                idempotencyKey,
                [
                    new JournalLineDraft(
                        FinancialAccountCode.PlatformCash,
                        order.TotalAmount,
                        0m,
                        FinancialOwnerType.Platform,
                        financialSettings.Value.PlatformWalletOwnerId,
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

            return posting.JournalEntryId;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "[BankTransfer] Ledger posting failed after confirming order {OrderId} payment {PaymentId}.",
                order.Id,
                payment.Id);

            return null;
        }
    }

    private void ValidateWebhookSecret()
    {
        var configuredSecret = settings.Value.WebhookSecret;
        if (string.IsNullOrWhiteSpace(configuredSecret))
        {
            if (environment.IsDevelopment())
            {
                return;
            }

            throw new BusinessRuleException(
                "BANK_TRANSFER_WEBHOOK_NOT_CONFIGURED",
                "Bank transfer webhook secret is not configured.");
        }

        var providedSecret = Request.Headers[WebhookSecretHeader].ToString();
        if (!FixedTimeEqualsString(configuredSecret.Trim(), providedSecret?.Trim() ?? string.Empty))
        {
            throw new BusinessRuleException(
                "BANK_TRANSFER_WEBHOOK_INVALID_SECRET",
                "Bank transfer webhook secret is invalid.");
        }
    }

    private static bool FixedTimeEqualsString(string expected, string provided)
    {
        var expectedBytes = System.Text.Encoding.UTF8.GetBytes(expected);
        var providedBytes = System.Text.Encoding.UTF8.GetBytes(provided);
        return expectedBytes.Length == providedBytes.Length
            && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }

    private static bool IsPaidWebhookStatus(string? status) =>
        string.Equals(status, "paid", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, "settled", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, "confirmed", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase);

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
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

public record BankTransferWebhookRequest(
    string? Reference,
    string? PaymentReference,
    string? BankReference,
    string? TransactionId,
    decimal? Amount,
    string? Currency,
    string? Status,
    DateTime? PaidAtUtc);

public record BankTransferConfirmationResult(
    string Message,
    Guid PaymentId,
    Guid OrderId,
    string OrderStatus,
    string PaymentStatus,
    Guid? JournalEntryId);
