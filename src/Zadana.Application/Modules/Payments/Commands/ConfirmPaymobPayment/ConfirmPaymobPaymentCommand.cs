using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Orders.Events;
using Zadana.Application.Modules.Orders.Support;
using Zadana.Application.Modules.Payments.DTOs;
using Zadana.Application.Modules.Payments.Interfaces;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Payments.Commands.ConfirmPaymobPayment;

public record ConfirmPaymobPaymentCommand(
    Guid? PaymentId,
    string? Payload,
    string? ProviderReference,
    string? ProviderTransactionId,
    bool? IsSuccess,
    bool? IsPending,
    string? CustomerDeviceId = null) : IRequest<PaymobPaymentConfirmationResultDto>;

public class ConfirmPaymobPaymentCommandValidator : AbstractValidator<ConfirmPaymobPaymentCommand>
{
    public ConfirmPaymobPaymentCommandValidator()
    {
        RuleFor(x => x)
            .Must(x =>
                x.PaymentId.HasValue ||
                !string.IsNullOrWhiteSpace(x.Payload) ||
                !string.IsNullOrWhiteSpace(x.ProviderReference) ||
                !string.IsNullOrWhiteSpace(x.ProviderTransactionId))
            .WithMessage("Payment id, payload, provider reference, or provider transaction id is required.");
    }
}

public class ConfirmPaymobPaymentCommandHandler : IRequestHandler<ConfirmPaymobPaymentCommand, PaymobPaymentConfirmationResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IPaymobGateway _paymobGateway;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublisher _publisher;

    public ConfirmPaymobPaymentCommandHandler(
        IApplicationDbContext context,
        IPaymobGateway paymobGateway,
        IUnitOfWork unitOfWork,
        IPublisher publisher)
    {
        _context = context;
        _paymobGateway = paymobGateway;
        _unitOfWork = unitOfWork;
        _publisher = publisher;
    }

    public async Task<PaymobPaymentConfirmationResultDto> Handle(ConfirmPaymobPaymentCommand request, CancellationToken cancellationToken)
    {
        var notification = ResolveNotification(request);
        if (notification.IsSuccess)
        {
            return await HandleSuccessfulConfirmationAsync(request, notification, cancellationToken);
        }

        var failedPayment = await ResolvePaymentAsync(request, notification, cancellationToken);
        ApplyProviderReferenceIfNeeded(failedPayment, notification.ProviderReference);
        var failedOrder = failedPayment.Order;

        if (IsAlreadyConfirmed(failedPayment, failedOrder))
        {
            return BuildResult(failedPayment, failedOrder, LocalizedMessages.GetAr(LocalizedMessages.PaymentAlreadyConfirmed), LocalizedMessages.GetEn(LocalizedMessages.PaymentAlreadyConfirmed), true);
        }

        if (!notification.IsPending && failedPayment.Status != PaymentStatus.Paid && failedPayment.Status != PaymentStatus.Failed)
        {
            failedPayment.MarkAsFailed("Paymob reported payment failure.", notification.ProviderTransactionId);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new PaymobPaymentConfirmationResultDto(
            notification.IsPending ? LocalizedMessages.GetAr(LocalizedMessages.PaymentStillPending) : LocalizedMessages.GetAr(LocalizedMessages.PaymentConfirmationFailed),
            notification.IsPending ? LocalizedMessages.GetEn(LocalizedMessages.PaymentStillPending) : LocalizedMessages.GetEn(LocalizedMessages.PaymentConfirmationFailed),
            failedPayment.Id,
            ToApiToken(failedPayment.Status.ToString()),
            failedOrder.UserId,
            failedOrder.Id,
            ToApiToken(failedOrder.Status.ToString()),
            false);
    }

    private async Task<PaymobPaymentConfirmationResultDto> HandleSuccessfulConfirmationAsync(
        ConfirmPaymobPaymentCommand request,
        PaymobWebhookNotificationDto notification,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 2;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var payment = await ResolvePaymentAsync(request, notification, cancellationToken);
            ApplyProviderReferenceIfNeeded(payment, notification.ProviderReference);

            var order = payment.Order;
            var originalOrderStatus = order.Status;
            var alreadyConfirmed = IsAlreadyConfirmed(payment, order);

            if (!alreadyConfirmed)
            {
                if (payment.Status != PaymentStatus.Paid)
                {
                    payment.MarkAsPaid(notification.ProviderTransactionId);
                }

                EnsureVendorAcceptanceTransition(order);
                OrderStatusHistoryTracking.TrackNewEntries(_context, order);
            }

            var shouldPublishVendorPlacement = ShouldPublishVendorPlacement(
                originalOrderStatus,
                order.Status,
                alreadyConfirmed);

            try
            {
                if (!alreadyConfirmed)
                {
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }
            }
            catch (DbUpdateConcurrencyException) when (attempt < maxAttempts - 1)
            {
                ResetTrackedState();
                continue;
            }
            catch (DbUpdateConcurrencyException) when (attempt == maxAttempts - 1)
            {
                ResetTrackedState();
                return await RecoverSuccessfulConfirmationAfterConcurrencyAsync(request, notification, cancellationToken);
            }

            await ClearCustomerCartAsync(order.UserId, request.CustomerDeviceId ?? payment.CheckoutDeviceId, cancellationToken);

            if (shouldPublishVendorPlacement)
            {
                await PublishVendorPlacementAsync(order, originalOrderStatus, cancellationToken);
            }

            return BuildResult(
                payment,
                order,
                alreadyConfirmed ? LocalizedMessages.GetAr(LocalizedMessages.PaymentAlreadyConfirmed) : LocalizedMessages.GetAr(LocalizedMessages.PaymentConfirmedSuccess),
                alreadyConfirmed ? LocalizedMessages.GetEn(LocalizedMessages.PaymentAlreadyConfirmed) : LocalizedMessages.GetEn(LocalizedMessages.PaymentConfirmedSuccess),
                alreadyConfirmed);
        }

        throw new InvalidOperationException("Paymob payment confirmation could not be completed.");
    }

    private async Task<PaymobPaymentConfirmationResultDto> RecoverSuccessfulConfirmationAfterConcurrencyAsync(
        ConfirmPaymobPaymentCommand request,
        PaymobWebhookNotificationDto notification,
        CancellationToken cancellationToken)
    {
        ResetTrackedState();

        var payment = await ResolvePaymentAsync(request, notification, cancellationToken);
        var order = payment.Order;
        var originalOrderStatus = order.Status;
        var alreadyConfirmed = IsAlreadyConfirmed(payment, order);
        var normalizedProviderTransactionId = string.IsNullOrWhiteSpace(notification.ProviderTransactionId)
            ? null
            : notification.ProviderTransactionId.Trim();
        var desiredStatus = ResolveDesiredOperationalStatus(order.Status, payment.Method);
        var shouldPublishVendorPlacement = ShouldPublishVendorPlacement(
            originalOrderStatus,
            desiredStatus,
            alreadyConfirmed);

        if (!alreadyConfirmed)
        {
            var utcNow = DateTime.UtcNow;

            var paymentQuery = _context.Payments.Where(x => x.Id == payment.Id);
            if (!string.IsNullOrWhiteSpace(normalizedProviderTransactionId))
            {
                await paymentQuery.ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.Status, PaymentStatus.Paid)
                        .SetProperty(x => x.PaidAtUtc, utcNow)
                        .SetProperty(x => x.ProviderTransactionId, normalizedProviderTransactionId)
                        .SetProperty(x => x.UpdatedAtUtc, utcNow),
                    cancellationToken);
            }
            else
            {
                await paymentQuery.ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.Status, PaymentStatus.Paid)
                        .SetProperty(x => x.PaidAtUtc, utcNow)
                        .SetProperty(x => x.UpdatedAtUtc, utcNow),
                    cancellationToken);
            }

            await _context.Orders
                .Where(x => x.Id == order.Id)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.PaymentStatus, PaymentStatus.Paid)
                        .SetProperty(
                            x => x.Status,
                            x => x.Status == OrderStatus.PendingPayment || x.Status == OrderStatus.Placed
                                ? desiredStatus
                                : x.Status)
                        .SetProperty(x => x.UpdatedAtUtc, utcNow),
                    cancellationToken);
        }

        ResetTrackedState();

        var latestPayment = await ResolvePaymentAsync(request, notification, cancellationToken);
        var latestOrder = latestPayment.Order;

        if (!IsAlreadyConfirmed(latestPayment, latestOrder))
        {
            throw new DbUpdateConcurrencyException("Paymob payment confirmation could not be reconciled after a concurrent update.");
        }

        await ClearCustomerCartAsync(latestOrder.UserId, request.CustomerDeviceId ?? latestPayment.CheckoutDeviceId, cancellationToken);

        if (shouldPublishVendorPlacement)
        {
            await PublishVendorPlacementAsync(latestOrder, originalOrderStatus, cancellationToken);
        }

        return BuildResult(
            latestPayment,
            latestOrder,
            alreadyConfirmed ? LocalizedMessages.GetAr(LocalizedMessages.PaymentAlreadyConfirmed) : LocalizedMessages.GetAr(LocalizedMessages.PaymentConfirmedSuccess),
            alreadyConfirmed ? LocalizedMessages.GetEn(LocalizedMessages.PaymentAlreadyConfirmed) : LocalizedMessages.GetEn(LocalizedMessages.PaymentConfirmedSuccess),
            alreadyConfirmed);
    }

    private PaymobWebhookNotificationDto ResolveNotification(ConfirmPaymobPaymentCommand request)
    {
        if (!string.IsNullOrWhiteSpace(request.Payload))
        {
            return _paymobGateway.ParseWebhookNotification(request.Payload);
        }

        return new PaymobWebhookNotificationDto(
            request.PaymentId,
            request.ProviderReference,
            request.ProviderTransactionId,
            request.IsSuccess ?? false,
            request.IsPending ?? false,
            "RETURN");
    }

    private async Task<Zadana.Domain.Modules.Payments.Entities.Payment> ResolvePaymentAsync(
        ConfirmPaymobPaymentCommand request,
        PaymobWebhookNotificationDto notification,
        CancellationToken cancellationToken)
    {
        var payment = notification.PaymentId.HasValue
            ? await _context.Payments
                .Include(x => x.Order)
                .FirstOrDefaultAsync(x => x.Id == notification.PaymentId.Value, cancellationToken)
            : null;

        if (payment == null && !string.IsNullOrWhiteSpace(notification.ProviderReference))
        {
            payment = await _context.Payments
                .Include(x => x.Order)
                .FirstOrDefaultAsync(
                    x => x.ProviderName == "Paymob" && x.ProviderTransactionId == notification.ProviderReference,
                    cancellationToken);
        }

        var providerTransactionLookup = !string.IsNullOrWhiteSpace(notification.ProviderTransactionId)
            ? notification.ProviderTransactionId
            : request.ProviderTransactionId;

        if (payment == null && !string.IsNullOrWhiteSpace(providerTransactionLookup))
        {
            var normalizedProviderTransactionId = providerTransactionLookup.Trim();
            payment = await _context.Payments
                .Include(x => x.Order)
                .FirstOrDefaultAsync(
                    x => x.ProviderName == "Paymob" && x.ProviderTransactionId == normalizedProviderTransactionId,
                    cancellationToken);
        }

        if (payment == null && request.PaymentId.HasValue)
        {
            payment = await _context.Payments
                .Include(x => x.Order)
                .FirstOrDefaultAsync(x => x.Id == request.PaymentId.Value, cancellationToken);
        }

        if (payment == null)
        {
            var lookupId = notification.PaymentId?.ToString() ??
                           request.PaymentId?.ToString() ??
                           notification.ProviderReference ??
                           notification.ProviderTransactionId ??
                           "unknown";
            throw new NotFoundException("Payment", lookupId);
        }

        return payment;
    }

    private static void ApplyProviderReferenceIfNeeded(
        Zadana.Domain.Modules.Payments.Entities.Payment payment,
        string? providerReference)
    {
        if (!string.IsNullOrWhiteSpace(providerReference) &&
            !string.Equals(payment.ProviderTransactionId, providerReference, StringComparison.Ordinal))
        {
            payment.SetProviderTransactionId(providerReference);
        }
    }

    private static bool IsAlreadyConfirmed(
        Zadana.Domain.Modules.Payments.Entities.Payment payment,
        Zadana.Domain.Modules.Orders.Entities.Order order) =>
        payment.Status == PaymentStatus.Paid &&
        order.Status == OrderStatus.PendingVendorAcceptance;

    private static bool ShouldPublishVendorPlacement(
        OrderStatus oldStatus,
        OrderStatus newStatus,
        bool alreadyConfirmed) =>
        !alreadyConfirmed &&
        newStatus == OrderStatus.PendingVendorAcceptance &&
        oldStatus != OrderStatus.PendingVendorAcceptance;

    private static void EnsureVendorAcceptanceTransition(Zadana.Domain.Modules.Orders.Entities.Order order)
    {
        var desiredStatus = ResolveDesiredOperationalStatus(order.Status, order.PaymentMethod);
        if (desiredStatus != order.Status)
        {
            order.ChangeStatus(desiredStatus, null, "Online payment confirmed and awaiting vendor response");
        }
    }

    private static OrderStatus ResolveDesiredOperationalStatus(OrderStatus currentStatus, PaymentMethodType paymentMethod)
    {
        if (currentStatus is OrderStatus.PendingPayment or OrderStatus.Placed)
        {
            return paymentMethod == PaymentMethodType.Card
                ? OrderStatus.PendingVendorAcceptance
                : OrderStatus.Placed;
        }

        return currentStatus;
    }

    private Task PublishVendorPlacementAsync(
        Zadana.Domain.Modules.Orders.Entities.Order order,
        OrderStatus oldStatus,
        CancellationToken cancellationToken) =>
        _publisher.Publish(
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

    private async Task ClearCustomerCartAsync(Guid userId, string? deviceId, CancellationToken cancellationToken)
    {
        var normalizedDeviceId = string.IsNullOrWhiteSpace(deviceId) ? null : deviceId.Trim();
        const int maxAttempts = 2;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
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

            var carts = userCarts
                .Concat(guestCarts)
                .GroupBy(x => x.Id)
                .Select(group => group.First())
                .ToList();

            if (carts.Count == 0)
            {
                return;
            }

            var cartItems = carts
                .SelectMany(x => x.Items)
                .ToList();

            if (cartItems.Count > 0)
            {
                _context.CartItems.RemoveRange(cartItems);
            }

            _context.Carts.RemoveRange(carts);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateConcurrencyException) when (attempt < maxAttempts - 1)
            {
                ResetTrackedState();
            }
            catch (DbUpdateConcurrencyException) when (attempt == maxAttempts - 1)
            {
                ResetTrackedState();
                return;
            }
        }
    }

    private void ResetTrackedState()
    {
        if (_context is DbContext dbContext)
        {
            dbContext.ChangeTracker.Clear();
        }
    }

    private static PaymobPaymentConfirmationResultDto BuildResult(
        Zadana.Domain.Modules.Payments.Entities.Payment payment,
        Zadana.Domain.Modules.Orders.Entities.Order order,
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
