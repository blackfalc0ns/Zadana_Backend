using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.EmailCenter;
using Zadana.Application.Modules.EmailCenter.DTOs;
using Zadana.Application.Modules.EmailCenter.Interfaces;
using Zadana.Application.Modules.Finances.Services;
using Zadana.Application.Modules.Orders.Events;
using Zadana.Application.Modules.Orders.Support;
using Zadana.Application.Modules.Payments.DTOs;
using Zadana.Application.Modules.Payments.Gateways;
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
    private readonly IEmailCenterService _emailCenterService;
    private readonly IDeliveryDispatchService _deliveryDispatchService;
    private readonly ILogger<ConfirmCardPaymentCommandHandler> _logger;

    public ConfirmCardPaymentCommandHandler(
        IApplicationDbContext context,
        IPaymentGatewayResolver gatewayResolver,
        IUnitOfWork unitOfWork,
        IPublisher publisher,
        OnlinePaymentCaptureService captureService,
        IEmailCenterService emailCenterService,
        IDeliveryDispatchService deliveryDispatchService,
        ILogger<ConfirmCardPaymentCommandHandler> logger)
    {
        _context = context;
        _gatewayResolver = gatewayResolver;
        _unitOfWork = unitOfWork;
        _publisher = publisher;
        _captureService = captureService;
        _emailCenterService = emailCenterService;
        _deliveryDispatchService = deliveryDispatchService;
        _logger = logger;
    }

    public async Task<CardPaymentConfirmationResultDto> Handle(ConfirmCardPaymentCommand request, CancellationToken cancellationToken)
    {
        var providerName = string.IsNullOrWhiteSpace(request.ProviderName) ? "Moyasar" : request.ProviderName!.Trim();
        var gateway = _gatewayResolver.Resolve(providerName);

        var requestedProviderPaymentId = string.IsNullOrWhiteSpace(request.ProviderPaymentId)
            ? null
            : request.ProviderPaymentId!.Trim();

        var payment = await TryResolvePaymentAsync(request, cancellationToken);
        GatewayPaymentDetails? details = null;

        if (payment is null && requestedProviderPaymentId is not null)
        {
            details = await gateway.FetchPaymentAsync(requestedProviderPaymentId, cancellationToken);
            payment = await ResolvePaymentFromProviderMetadataAsync(details, providerName, cancellationToken);
        }

        if (payment is null)
        {
            var lookup = request.PaymentId?.ToString() ?? requestedProviderPaymentId ?? "unknown";
            throw new NotFoundException("Payment", lookup);
        }

        var order = payment.Order;

        // If the caller didn't pass a provider payment id, but we have one cached on the payment, use that.
        var providerPaymentId = requestedProviderPaymentId ?? payment.ProviderTransactionId;

        if (string.IsNullOrWhiteSpace(providerPaymentId))
        {
            // Nothing to verify yet (e.g. customer returned before completing the form).
            return BuildResult(payment, order, LocalizedMessages.GetAr(LocalizedMessages.PaymentStillPending), LocalizedMessages.GetEn(LocalizedMessages.PaymentStillPending), false);
        }

        details ??= await gateway.FetchPaymentAsync(providerPaymentId, cancellationToken);

        if (IsDeliveryUpgradePayment(details.Metadata))
        {
            return await ApplyDeliveryUpgradePaymentAsync(payment, order, details, request.CustomerDeviceId, cancellationToken);
        }

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
            await DispatchPaymentFailureEmailAsync(order, details.FailureMessage, cancellationToken);

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
            await EnsurePaymentReservationCanStillBeConfirmedAsync(payment, order, cancellationToken);

            if (payment.Status != PaymentStatus.Paid)
            {
                payment.MarkAsPaid(details.ProviderPaymentId);
            }

            EnsureVendorAcceptanceTransition(order);
            OrderStatusHistoryTracking.TrackNewEntries(_context, order);
            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (_context is DbContext dbContext)
                {
                    await dbContext.Entry(payment).ReloadAsync(cancellationToken);
                    await dbContext.Entry(order).ReloadAsync(cancellationToken);
                }

                if (!IsAlreadyConfirmed(payment, order))
                {
                    throw new BusinessRuleException(
                        "ORDER_PAYMENT_RESERVATION_EXPIRED",
                        "This payment can no longer be confirmed because the order reservation expired.");
                }

                alreadyConfirmed = true;
            }
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

    private async Task DispatchPaymentFailureEmailAsync(
        Domain.Modules.Orders.Entities.Order order,
        string? failureMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            var emailData = await _context.Orders
                .AsNoTracking()
                .Where(item => item.Id == order.Id)
                .Select(item => new
                {
                    item.OrderNumber,
                    item.UserId,
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

            await _emailCenterService.DispatchSystemEventEmailAsync(
                new EmailSystemEventDispatchRequest(
                    EventKey: EmailEventKeys.CustomerOrderImportantUpdate,
                    AudienceType: "customers",
                    To: string.IsNullOrWhiteSpace(emailData.CustomerEmail) ? [] : [emailData.CustomerEmail],
                    Variables: new Dictionary<string, string>
                    {
                        ["customer_name"] = string.IsNullOrWhiteSpace(emailData.CustomerName) ? "Customer" : emailData.CustomerName,
                        ["order_number"] = emailData.OrderNumber,
                        ["vendor_name"] = emailData.VendorName,
                        ["update_message"] = string.IsNullOrWhiteSpace(failureMessage)
                            ? $"Payment failed for order {emailData.OrderNumber}. Please retry payment from the app."
                            : $"Payment failed for order {emailData.OrderNumber}: {failureMessage}"
                    },
                    TargetUrl: OrderStatusNotificationComposer.ResolveTargetUrl(order.Id),
                    EntityId: order.Id,
                    RecipientEntityId: emailData.UserId,
                    VendorId: order.VendorId),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ConfirmCardPayment] Failed to dispatch payment failure email for order {OrderId}.", order.Id);
        }
    }

    private async Task<Domain.Modules.Payments.Entities.Payment?> TryResolvePaymentAsync(
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

        return payment;
    }

    private async Task<Domain.Modules.Payments.Entities.Payment?> ResolvePaymentFromProviderMetadataAsync(
        GatewayPaymentDetails details,
        string providerName,
        CancellationToken cancellationToken)
    {
        if (TryGetMetadataValue(details.Metadata, "payment_id", out var metadataPaymentId)
            && Guid.TryParse(metadataPaymentId, out var paymentId))
        {
            var payment = await _context.Payments
                .Include(x => x.Order)
                .FirstOrDefaultAsync(
                    x => x.Id == paymentId && x.ProviderName == providerName,
                    cancellationToken);

            if (payment is not null)
            {
                return payment;
            }
        }

        if (TryGetMetadataValue(details.Metadata, "order_id", out var metadataOrderId)
            && Guid.TryParse(metadataOrderId, out var orderId))
        {
            return await _context.Payments
                .Include(x => x.Order)
                .Where(x =>
                    x.OrderId == orderId &&
                    x.ProviderName == providerName &&
                    (x.Method == PaymentMethodType.Card
                     || x.Method == PaymentMethodType.ApplePay
                     || x.Method == PaymentMethodType.Mada))
                .OrderByDescending(x => x.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return null;
    }

    private static bool TryGetMetadataValue(
        IReadOnlyDictionary<string, string> metadata,
        string key,
        out string value)
    {
        if (metadata.TryGetValue(key, out value!))
        {
            return true;
        }

        foreach (var pair in metadata)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = pair.Value;
                return true;
            }
        }

        value = string.Empty;
        return false;
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

    private async Task EnsurePaymentReservationCanStillBeConfirmedAsync(
        Domain.Modules.Payments.Entities.Payment payment,
        Domain.Modules.Orders.Entities.Order order,
        CancellationToken cancellationToken)
    {
        var currentPaymentStatus = payment.Status;
        if (_context is DbContext dbContext)
        {
            await dbContext.Entry(order).ReloadAsync(cancellationToken);
            currentPaymentStatus = await _context.Payments
                .AsNoTracking()
                .Where(item => item.Id == payment.Id)
                .Select(item => item.Status)
                .FirstAsync(cancellationToken);
        }

        if (currentPaymentStatus == PaymentStatus.Failed ||
            order.PaymentStatus == PaymentStatus.Failed ||
            order.Status == OrderStatus.Cancelled)
        {
            throw new BusinessRuleException(
                "ORDER_PAYMENT_RESERVATION_EXPIRED",
                "This payment can no longer be confirmed because the order reservation expired.");
        }

        if (order.Status is not (OrderStatus.PendingPayment or OrderStatus.Placed or OrderStatus.PendingVendorAcceptance))
        {
            throw new BusinessRuleException(
                "ORDER_PAYMENT_CONFIRMATION_NOT_ALLOWED",
                $"Payment cannot be confirmed while order is in {order.Status}.");
        }
    }

    private static void EnsureVendorAcceptanceTransition(Domain.Modules.Orders.Entities.Order order)
    {
        if (order.Status is OrderStatus.PendingPayment or OrderStatus.Placed)
        {
            var nextStatus = order.PaymentMethod.IsOnlineGatewayMethod()
                ? OrderStatus.PendingVendorAcceptance
                : OrderStatus.Placed;
            order.ChangeStatus(
                nextStatus,
                null,
                nextStatus == OrderStatus.PendingVendorAcceptance
                    ? "Online payment confirmed and awaiting vendor response"
                    : "Payment confirmed");
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

    private async Task<CardPaymentConfirmationResultDto> ApplyDeliveryUpgradePaymentAsync(
        Domain.Modules.Payments.Entities.Payment payment,
        Domain.Modules.Orders.Entities.Order order,
        Gateways.GatewayPaymentDetails details,
        string? customerDeviceId,
        CancellationToken cancellationToken)
    {
        if (order.DeliveryUpgradePaymentId != payment.Id)
        {
            throw new BusinessRuleException(
                "PAYMENT_ORDER_MISMATCH",
                "Delivery upgrade payment does not match the order upgrade session.");
        }

        CurrencyPolicy.EnsureOfficial(details.Currency);
        var expectedMinor = CurrencyPolicy.ToMinorUnits(payment.Amount, payment.Currency);
        if (details.AmountMinorUnits != expectedMinor)
        {
            throw new BusinessRuleException(
                "PAYMENT_AMOUNT_MISMATCH",
                $"Provider amount {details.AmountMinorUnits} (minor units) does not match upgrade amount {expectedMinor}.");
        }

        if (TryGetMetadataValue(details.Metadata, "originalOrderId", out var originalOrderId)
            && !string.Equals(originalOrderId, order.Id.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException(
                "PAYMENT_ORDER_MISMATCH",
                "Provider metadata.originalOrderId does not match the local order.");
        }

        payment.ApplyProviderFetch(details.ProviderStatus, details.ProviderReferenceNumber, details.RawResponse);
        payment.SetProviderTransactionId(details.ProviderPaymentId);

        if (!IsPaidStatus(details.ProviderStatus))
        {
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

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return BuildResult(
                payment,
                order,
                LocalizedMessages.GetAr(LocalizedMessages.PaymentStillPending),
                LocalizedMessages.GetEn(LocalizedMessages.PaymentStillPending),
                false);
        }

        var alreadyConverted = order.ConvertedToDeliveryAtUtc.HasValue && payment.Status == PaymentStatus.Paid;
        if (!alreadyConverted)
        {
            if (payment.Status != PaymentStatus.Paid)
            {
                payment.MarkAsPaid(details.ProviderPaymentId);
            }

            var customerAddressId = ReadMetadataGuid(details.Metadata, "customer_address_id")
                ?? throw new BusinessRuleException("DELIVERY_UPGRADE_METADATA_MISSING", "Delivery upgrade metadata is missing customer_address_id.");
            var reason = ReadMetadataEnum(details.Metadata, "reason") ?? ConvertToDeliveryReason.CustomerRequest;
            var deliveryFee = ReadMetadataDecimal(details.Metadata, "delivery_fee") ?? payment.Amount;
            var baseDeliveryFee = ReadMetadataDecimal(details.Metadata, "base_delivery_fee") ?? 0m;
            var distanceDeliveryFee = ReadMetadataDecimal(details.Metadata, "distance_delivery_fee") ?? 0m;
            var surgeDeliveryFee = ReadMetadataDecimal(details.Metadata, "surge_delivery_fee") ?? 0m;
            var changedByUserId = ReadMetadataGuid(details.Metadata, "changed_by_user_id");

            var oldStatus = order.Status;
            order.ConvertToDelivery(
                customerAddressId,
                deliveryFee,
                baseDeliveryFee,
                distanceDeliveryFee,
                surgeDeliveryFee,
                changedByUserId,
                reason);
            order.ApplyDeliveryFeeDeltaPaid(payment.Amount);
            OrderStatusHistoryTracking.TrackNewEntries(_context, order);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            if (oldStatus != order.Status)
            {
                await _publisher.Publish(
                    new OrderStatusChangedNotification(
                        order.Id,
                        order.UserId,
                        order.VendorId,
                        order.OrderNumber,
                        oldStatus,
                        order.Status,
                        NotifyCustomer: true,
                        NotifyVendor: true,
                        ActorRole: "payment_gateway"),
                    cancellationToken);
            }

            await _deliveryDispatchService.TryAutoDispatchAsync(order.Id, cancellationToken: cancellationToken);
        }

        return BuildResult(
            payment,
            order,
            alreadyConverted
                ? LocalizedMessages.GetAr(LocalizedMessages.PaymentAlreadyConfirmed)
                : LocalizedMessages.GetAr(LocalizedMessages.PaymentConfirmedSuccess),
            alreadyConverted
                ? LocalizedMessages.GetEn(LocalizedMessages.PaymentAlreadyConfirmed)
                : LocalizedMessages.GetEn(LocalizedMessages.PaymentConfirmedSuccess),
            alreadyConverted);
    }

    private static bool IsDeliveryUpgradePayment(IReadOnlyDictionary<string, string> metadata) =>
        TryGetMetadataValue(metadata, "kind", out var kind) &&
        string.Equals(kind, "delivery_upgrade", StringComparison.OrdinalIgnoreCase);

    private static Guid? ReadMetadataGuid(IReadOnlyDictionary<string, string> metadata, string key)
    {
        if (!TryGetMetadataValue(metadata, key, out var value) || !Guid.TryParse(value, out var parsed))
        {
            return null;
        }

        return parsed;
    }

    private static decimal? ReadMetadataDecimal(IReadOnlyDictionary<string, string> metadata, string key)
    {
        if (!TryGetMetadataValue(metadata, key, out var value))
        {
            return null;
        }

        return decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static ConvertToDeliveryReason? ReadMetadataEnum(IReadOnlyDictionary<string, string> metadata, string key)
    {
        if (!TryGetMetadataValue(metadata, key, out var value))
        {
            return null;
        }

        return Enum.TryParse<ConvertToDeliveryReason>(value, true, out var parsed) ? parsed : null;
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
