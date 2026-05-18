using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Finances.Services;
using Zadana.Application.Modules.Orders.Events;
using Zadana.Application.Modules.Orders.Support;
using Zadana.Application.Modules.Payments.DTOs;
using Zadana.Application.Modules.Payments.Interfaces;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Finance;

namespace Zadana.Application.Modules.Payments.Commands.ConfirmCardPayment;

/// <summary>
/// Confirms a card payment by fetching the authoritative payment state from the
/// gateway (Moyasar) and reconciling it against the local <c>Payment</c> + <c>Order</c>.
/// Used by both the customer-facing return URL handler and the webhook processor.
/// </summary>
public record ConfirmCardPaymentCommand(
    Guid? PaymentId,
    string? ProviderPaymentId,
    string? ProviderName = "Moyasar",
    string? CustomerDeviceId = null) : IRequest<CardPaymentConfirmationResultDto>;

public class ConfirmCardPaymentCommandValidator : AbstractValidator<ConfirmCardPaymentCommand>
{
    public ConfirmCardPaymentCommandValidator()
    {
        RuleFor(x => x)
            .Must(x => x.PaymentId.HasValue || !string.IsNullOrWhiteSpace(x.ProviderPaymentId))
            .WithMessage("Payment id or provider payment id is required.");
    }
}

public class ConfirmCardPaymentCommandHandler : IRequestHandler<ConfirmCardPaymentCommand, CardPaymentConfirmationResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IPaymentGatewayResolver _gatewayResolver;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublisher _publisher;
    private readonly OnlinePaymentCaptureService _captureService;
    private readonly ILogger<ConfirmCardPaymentCommandHandler> _logger;

    public ConfirmCardPaymentCommandHandler(
        IApplicationDbContext context,
        IPaymentGatewayResolver gatewayResolver,
        IUnitOfWork unitOfWork,
        IPublisher publisher,
        OnlinePaymentCaptureService captureService,
        ILogger<ConfirmCardPaymentCommandHandler> logger)
    {
        _context = context;
        _gatewayResolver = gatewayResolver;
        _unitOfWork = unitOfWork;
        _publisher = publisher;
        _captureService = captureService;
        _logger = logger;
    }

    public async Task<CardPaymentConfirmationResultDto> Handle(ConfirmCardPaymentCommand request, CancellationToken cancellationToken)
    {
        var providerName = string.IsNullOrWhiteSpace(request.ProviderName) ? "Moyasar" : request.ProviderName!.Trim();
        var gateway = _gatewayResolver.Resolve(providerName);

        var payment = await ResolvePaymentAsync(request, cancellationToken);
        var order = payment.Order;

        // If the caller didn't pass a provider payment id, but we have one cached on the payment, use that.
        var providerPaymentId = string.IsNullOrWhiteSpace(request.ProviderPaymentId)
            ? payment.ProviderTransactionId
            : request.ProviderPaymentId!.Trim();

        if (string.IsNullOrWhiteSpace(providerPaymentId))
        {
            // Nothing to verify yet (e.g. customer returned before completing the form).
            return BuildResult(payment, order, LocalizedMessages.GetAr(LocalizedMessages.PaymentStillPending), LocalizedMessages.GetEn(LocalizedMessages.PaymentStillPending), false);
        }

        var details = await gateway.FetchPaymentAsync(providerPaymentId, cancellationToken);

        // Verification: currency, amount, metadata.order_id must match.
        CurrencyPolicy.EnsureOfficial(details.Currency);
        var expectedMinor = CurrencyPolicy.ToMinorUnits(order.TotalAmount, order.Currency);
        if (details.AmountMinorUnits != expectedMinor)
        {
            throw new BusinessRuleException(
                "PAYMENT_AMOUNT_MISMATCH",
                $"Provider amount {details.AmountMinorUnits} (minor units) does not match order amount {expectedMinor}.");
        }

        if (details.Metadata.TryGetValue("order_id", out var metaOrderId)
            && !string.Equals(metaOrderId, order.Id.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException(
                "PAYMENT_ORDER_MISMATCH",
                "Provider metadata.order_id does not match the local order.");
        }

        payment.ApplyProviderFetch(details.ProviderStatus, details.ProviderReferenceNumber, details.RawResponse);
        payment.SetProviderTransactionId(details.ProviderPaymentId);

        if (IsPaidStatus(details.ProviderStatus))
        {
            return await ApplySuccessfulPaymentAsync(payment, order, details, request.CustomerDeviceId, cancellationToken);
        }

        if (IsFailedStatus(details.ProviderStatus))
        {
            if (payment.Status is not (PaymentStatus.Paid or PaymentStatus.Failed))
            {
                payment.MarkAsFailed(details.FailureMessage ?? "Provider reported payment failure.", details.ProviderPaymentId);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return BuildResult(
                payment,
                order,
                LocalizedMessages.GetAr(LocalizedMessages.PaymentConfirmationFailed),
                LocalizedMessages.GetEn(LocalizedMessages.PaymentConfirmationFailed),
                false);
        }

        // Pending / authorized / verified - persist provider status without changing payment state.
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return BuildResult(
            payment,
            order,
            LocalizedMessages.GetAr(LocalizedMessages.PaymentStillPending),
            LocalizedMessages.GetEn(LocalizedMessages.PaymentStillPending),
            false);
    }

    private async Task<CardPaymentConfirmationResultDto> ApplySuccessfulPaymentAsync(
        Domain.Modules.Payments.Entities.Payment payment,
        Domain.Modules.Orders.Entities.Order order,
        Gateways.GatewayPaymentDetails details,
        string? customerDeviceId,
        CancellationToken cancellationToken)
    {
        var alreadyConfirmed = IsAlreadyConfirmed(payment, order);
        var originalOrderStatus = order.Status;

        if (!alreadyConfirmed)
        {
            if (payment.Status != PaymentStatus.Paid)
            {
                payment.MarkAsPaid(details.ProviderPaymentId);
            }

            EnsureVendorAcceptanceTransition(order);
            OrderStatusHistoryTracking.TrackNewEntries(_context, order);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // Post the OnlinePaymentCaptured ledger event idempotently.
        // Runs even on the "already confirmed" path so that any earlier failure
        // to write the journal entry (e.g. transient DB error after MarkAsPaid)
        // is healed on the next call without producing a duplicate posting.
        try
        {
            await _captureService.PostCapturedAsync(
                order,
                payment,
                details.ProviderName,
                details.ProviderPaymentId,
                cancellationToken);
        }
        catch (Exception ex)
        {
            // Capture posting must not roll back a confirmed payment. The order
            // is already marked paid; surface the failure to ops via logs and
            // let the next webhook retry / admin reconcile the ledger.
            _logger.LogError(
                ex,
                "[ConfirmCardPayment] Capture posting failed for order {OrderId} payment {PaymentId}.",
                order.Id, payment.Id);
        }

        await ClearCustomerCartAsync(order.UserId, customerDeviceId ?? payment.CheckoutDeviceId, cancellationToken);

        if (!alreadyConfirmed && order.Status == OrderStatus.PendingVendorAcceptance)
        {
            await _publisher.Publish(
                new OrderStatusChangedNotification(
                    order.Id,
                    order.UserId,
                    order.VendorId,
                    order.OrderNumber,
                    originalOrderStatus,
                    order.Status,
                    NotifyCustomer: true,
                    NotifyVendor: true,
                    ActorRole: "payment_gateway"),
                cancellationToken);
        }

        return BuildResult(
            payment,
            order,
            alreadyConfirmed ? LocalizedMessages.GetAr(LocalizedMessages.PaymentAlreadyConfirmed) : LocalizedMessages.GetAr(LocalizedMessages.PaymentConfirmedSuccess),
            alreadyConfirmed ? LocalizedMessages.GetEn(LocalizedMessages.PaymentAlreadyConfirmed) : LocalizedMessages.GetEn(LocalizedMessages.PaymentConfirmedSuccess),
            alreadyConfirmed);
    }

    private async Task<Domain.Modules.Payments.Entities.Payment> ResolvePaymentAsync(
        ConfirmCardPaymentCommand request,
        CancellationToken cancellationToken)
    {
        Domain.Modules.Payments.Entities.Payment? payment = null;

        if (request.PaymentId.HasValue)
        {
            payment = await _context.Payments
                .Include(x => x.Order)
                .FirstOrDefaultAsync(x => x.Id == request.PaymentId.Value, cancellationToken);
        }

        if (payment is null && !string.IsNullOrWhiteSpace(request.ProviderPaymentId))
        {
            var providerName = string.IsNullOrWhiteSpace(request.ProviderName) ? "Moyasar" : request.ProviderName!.Trim();
            var providerPaymentId = request.ProviderPaymentId.Trim();
            payment = await _context.Payments
                .Include(x => x.Order)
                .FirstOrDefaultAsync(
                    x => x.ProviderName == providerName && x.ProviderTransactionId == providerPaymentId,
                    cancellationToken);
        }

        if (payment is null)
        {
            var lookup = request.PaymentId?.ToString() ?? request.ProviderPaymentId ?? "unknown";
            throw new NotFoundException("Payment", lookup);
        }

        return payment;
    }

    private static bool IsPaidStatus(string status) =>
        string.Equals(status, "paid", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, "captured", StringComparison.OrdinalIgnoreCase);

    private static bool IsFailedStatus(string status) =>
        string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, "voided", StringComparison.OrdinalIgnoreCase);

    private static bool IsAlreadyConfirmed(
        Domain.Modules.Payments.Entities.Payment payment,
        Domain.Modules.Orders.Entities.Order order) =>
        payment.Status == PaymentStatus.Paid &&
        order.Status == OrderStatus.PendingVendorAcceptance;

    private static void EnsureVendorAcceptanceTransition(Domain.Modules.Orders.Entities.Order order)
    {
        if (order.Status is OrderStatus.PendingPayment or OrderStatus.Placed)
        {
            order.ChangeStatus(
                order.PaymentMethod == PaymentMethodType.Card ? OrderStatus.PendingVendorAcceptance : OrderStatus.Placed,
                null,
                "Online payment confirmed and awaiting vendor response");
        }
    }

    private async Task ClearCustomerCartAsync(Guid userId, string? deviceId, CancellationToken cancellationToken)
    {
        var normalizedDeviceId = string.IsNullOrWhiteSpace(deviceId) ? null : deviceId.Trim();

        var userCarts = await _context.Carts
            .Include(x => x.Items)
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);

        var guestCarts = normalizedDeviceId == null
            ? []
            : await _context.Carts
                .Include(x => x.Items)
                .Where(x => x.GuestId == normalizedDeviceId)
                .ToListAsync(cancellationToken);

        var carts = userCarts.Concat(guestCarts).GroupBy(x => x.Id).Select(group => group.First()).ToList();
        if (carts.Count == 0)
        {
            return;
        }

        var items = carts.SelectMany(x => x.Items).ToList();
        if (items.Count > 0) _context.CartItems.RemoveRange(items);
        _context.Carts.RemoveRange(carts);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another concurrent confirmation already cleared the cart.
        }
    }

    private static CardPaymentConfirmationResultDto BuildResult(
        Domain.Modules.Payments.Entities.Payment payment,
        Domain.Modules.Orders.Entities.Order order,
        string messageAr,
        string messageEn,
        bool alreadyConfirmed) =>
        new(
            messageAr,
            messageEn,
            payment.Id,
            ToApiToken(payment.Status.ToString()),
            order.UserId,
            order.Id,
            ToApiToken(order.Status.ToString()),
            alreadyConfirmed);

    private static string ToApiToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder(value.Length + 4);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (index > 0 && char.IsUpper(character))
            {
                builder.Append('_');
            }
            builder.Append(char.ToLowerInvariant(character));
        }
        return builder.ToString();
    }
}
