using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Orders.Events;
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
        var payment = await ResolvePaymentAsync(request, notification, cancellationToken);

        if (!string.IsNullOrWhiteSpace(notification.ProviderReference) &&
            !string.Equals(payment.ProviderTransactionId, notification.ProviderReference, StringComparison.Ordinal))
        {
            payment.SetProviderTransactionId(notification.ProviderReference);
        }

        var order = payment.Order;
        var originalOrderStatus = order.Status;
        var shouldPublishVendorPlacement = false;

        if (notification.IsSuccess)
        {
            if (payment.Status != PaymentStatus.Paid)
            {
                payment.MarkAsPaid(notification.ProviderTransactionId);
            }

            await ClearCustomerCartAsync(order.UserId, request.CustomerDeviceId ?? payment.CheckoutDeviceId, cancellationToken);

            if (order.Status == OrderStatus.Placed)
            {
                order.ChangeStatus(OrderStatus.PendingVendorAcceptance, null, "Online payment confirmed and awaiting vendor response");
            }

            shouldPublishVendorPlacement = order.Status == OrderStatus.PendingVendorAcceptance &&
                originalOrderStatus != OrderStatus.PendingVendorAcceptance;

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            if (shouldPublishVendorPlacement)
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

            var alreadyConfirmed = payment.Status == PaymentStatus.Paid &&
                originalOrderStatus == OrderStatus.PendingVendorAcceptance &&
                !shouldPublishVendorPlacement;

            return new PaymobPaymentConfirmationResultDto(
                alreadyConfirmed ? "Payment already confirmed." : "Payment confirmed successfully.",
                payment.Id,
                ToApiToken(payment.Status.ToString()),
                order.UserId,
                order.Id,
                ToApiToken(order.Status.ToString()),
                alreadyConfirmed);
        }

        if (!notification.IsPending && payment.Status != PaymentStatus.Paid && payment.Status != PaymentStatus.Failed)
        {
            payment.MarkAsFailed("Paymob reported payment failure.", notification.ProviderTransactionId);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new PaymobPaymentConfirmationResultDto(
            notification.IsPending ? "Payment is still pending." : "Payment confirmation failed.",
            payment.Id,
            ToApiToken(payment.Status.ToString()),
            order.UserId,
            order.Id,
            ToApiToken(order.Status.ToString()),
            false);
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
    }

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
