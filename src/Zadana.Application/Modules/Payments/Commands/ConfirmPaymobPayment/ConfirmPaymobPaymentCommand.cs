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
    Guid? CustomerUserId = null) : IRequest<PaymobPaymentConfirmationResultDto>;

public class ConfirmPaymobPaymentCommandValidator : AbstractValidator<ConfirmPaymobPaymentCommand>
{
    public ConfirmPaymobPaymentCommandValidator()
    {
        RuleFor(x => x)
            .Must(x => x.PaymentId.HasValue || !string.IsNullOrWhiteSpace(x.Payload))
            .WithMessage("Payment id or payload is required.");
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

        EnsureCustomerOwnsPayment(payment, request.CustomerUserId);

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

            await ClearCustomerCartAsync(order.UserId, cancellationToken);

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
                .ThenInclude(order => order.StatusHistory)
                .FirstOrDefaultAsync(x => x.Id == notification.PaymentId.Value, cancellationToken)
            : null;

        if (payment == null && !string.IsNullOrWhiteSpace(notification.ProviderReference))
        {
            payment = await _context.Payments
                .Include(x => x.Order)
                .ThenInclude(order => order.StatusHistory)
                .FirstOrDefaultAsync(
                    x => x.ProviderName == "Paymob" && x.ProviderTransactionId == notification.ProviderReference,
                    cancellationToken);
        }

        if (payment == null && request.PaymentId.HasValue)
        {
            payment = await _context.Payments
                .Include(x => x.Order)
                .ThenInclude(order => order.StatusHistory)
                .FirstOrDefaultAsync(x => x.Id == request.PaymentId.Value, cancellationToken);
        }

        if (payment == null)
        {
            var lookupId = notification.PaymentId?.ToString() ??
                           request.PaymentId?.ToString() ??
                           notification.ProviderReference ??
                           "unknown";
            throw new NotFoundException("Payment", lookupId);
        }

        return payment;
    }

    private static void EnsureCustomerOwnsPayment(Zadana.Domain.Modules.Payments.Entities.Payment payment, Guid? customerUserId)
    {
        if (!customerUserId.HasValue)
        {
            return;
        }

        if (payment.Order.UserId != customerUserId.Value)
        {
            throw new UnauthorizedException("PAYMENT_NOT_OWNED_BY_CUSTOMER");
        }
    }

    private async Task ClearCustomerCartAsync(Guid userId, CancellationToken cancellationToken)
    {
        var cart = await _context.Carts
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        if (cart != null)
        {
            _context.Carts.Remove(cart);
        }
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
