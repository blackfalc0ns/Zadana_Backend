using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Orders.Events;
using Zadana.Application.Modules.Orders.Services;
using Zadana.Application.Modules.Orders.Support;
using Zadana.Application.Modules.Payments.Gateways;
using Zadana.Application.Modules.Payments.Interfaces;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Entities;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Finance;

namespace Zadana.Application.Modules.Orders.Commands.ConvertOrderToDelivery;

public record ConvertOrderToDeliveryCommand(
    Guid OrderId,
    Guid? VendorId,
    Guid? AdminUserId,
    Guid CustomerAddressId,
    ConvertToDeliveryReason Reason) : IRequest<ConvertOrderToDeliveryResultDto>;

public record ConvertOrderToDeliveryResultDto(
    Guid OrderId,
    bool Converted,
    string? PaymentSessionUrl,
    Guid? PaymentId,
    string Status,
    string Message);

public class ConvertOrderToDeliveryCommandValidator : AbstractValidator<ConvertOrderToDeliveryCommand>
{
    public ConvertOrderToDeliveryCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.CustomerAddressId).NotEmpty();
        RuleFor(x => x)
            .Must(x => x.VendorId.HasValue || x.AdminUserId.HasValue)
            .WithMessage("VendorId or AdminUserId is required.");
    }
}

public class ConvertOrderToDeliveryCommandHandler : IRequestHandler<ConvertOrderToDeliveryCommand, ConvertOrderToDeliveryResultDto>
{
    private const string CardProvider = "Moyasar";

    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublisher _publisher;
    private readonly IDeliveryPricingService _deliveryPricingService;
    private readonly IDeliveryDispatchService _deliveryDispatchService;
    private readonly IPaymentGatewayResolver _gatewayResolver;

    public ConvertOrderToDeliveryCommandHandler(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IPublisher publisher,
        IDeliveryPricingService deliveryPricingService,
        IDeliveryDispatchService deliveryDispatchService,
        IPaymentGatewayResolver gatewayResolver)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _publisher = publisher;
        _deliveryPricingService = deliveryPricingService;
        _deliveryDispatchService = deliveryDispatchService;
        _gatewayResolver = gatewayResolver;
    }

    public async Task<ConvertOrderToDeliveryResultDto> Handle(
        ConvertOrderToDeliveryCommand request,
        CancellationToken cancellationToken)
    {
        var order = await LoadOrderAsync(request, cancellationToken);
        ValidatePreconditions(order);

        var address = await _context.CustomerAddresses
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.CustomerAddressId, cancellationToken)
            ?? throw new NotFoundException("CustomerAddress", request.CustomerAddressId);

        if (address.UserId != order.UserId)
        {
            throw new BusinessRuleException(
                "CUSTOMER_ADDRESS_NOT_OWNED",
                "The selected address does not belong to the order customer.");
        }

        if (!order.VendorBranchId.HasValue)
        {
            throw new BusinessRuleException(
                "DELIVERY_PRICING_UNAVAILABLE",
                "Delivery pricing could not be determined because the pickup branch is unknown.");
        }

        var deliveryQuote = await _deliveryPricingService.QuoteAsync(
            order.VendorBranchId.Value,
            address.Id,
            cancellationToken,
            order.Subtotal);

        var delta = decimal.Round(deliveryQuote.TotalFee - order.DeliveryFee, 2, MidpointRounding.AwayFromZero);
        var changedByUserId = request.AdminUserId ?? request.VendorId;

        if (delta > 0m)
        {
            return await CreateDeliveryUpgradePaymentAsync(
                order,
                address.Id,
                request.Reason,
                deliveryQuote,
                delta,
                changedByUserId,
                cancellationToken);
        }

        return await CompleteConversionAsync(
            order,
            address.Id,
            request.Reason,
            deliveryQuote,
            changedByUserId,
            cancellationToken);
    }

    private async Task<Order> LoadOrderAsync(ConvertOrderToDeliveryCommand request, CancellationToken cancellationToken)
    {
        var query = _context.Orders
            .Include(x => x.StatusHistory)
            .AsQueryable();

        if (request.VendorId.HasValue)
        {
            query = query.Where(item =>
                item.Id == request.OrderId &&
                item.VendorId == request.VendorId.Value);
        }
        else
        {
            query = query.Where(item => item.Id == request.OrderId);
        }

        return await query.FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Order", request.OrderId);
    }

    private static void ValidatePreconditions(Order order)
    {
        if (order.Fulfillment != FulfillmentType.Pickup)
        {
            throw new BusinessRuleException(
                "CONVERT_TO_DELIVERY_NOT_ALLOWED_IN_STATE",
                "Only pickup orders can be converted to delivery.");
        }

        if (order.PaymentStatus != PaymentStatus.Paid)
        {
            throw new BusinessRuleException(
                "ORDER_PAYMENT_NOT_CONFIRMED",
                "Order payment must be confirmed before converting to delivery.");
        }

        var allowed = order.Status is OrderStatus.Placed
            or OrderStatus.PendingVendorAcceptance
            or OrderStatus.Accepted
            or OrderStatus.Preparing
            or OrderStatus.ReadyForPickup;

        if (!allowed)
        {
            throw new BusinessRuleException(
                "CONVERT_TO_DELIVERY_NOT_ALLOWED_IN_STATE",
                $"Cannot convert to delivery while order is in {order.Status}.");
        }
    }

    private async Task<ConvertOrderToDeliveryResultDto> CreateDeliveryUpgradePaymentAsync(
        Order order,
        Guid customerAddressId,
        ConvertToDeliveryReason reason,
        DeliveryPriceQuote deliveryQuote,
        decimal delta,
        Guid? changedByUserId,
        CancellationToken cancellationToken)
    {
        if (!_gatewayResolver.TryResolve(CardProvider, out var gateway) || gateway is null)
        {
            throw new BusinessRuleException("PAYMENT_UNAVAILABLE", "Card checkout provider is disabled or not configured.");
        }

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == order.UserId, cancellationToken)
            ?? throw new NotFoundException("User", order.UserId);

        var payment = new Payment(order.Id, PaymentMethodType.Card, delta);
        _context.Payments.Add(payment);
        order.AttachDeliveryUpgradePayment(payment.Id);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        CurrencyPolicy.EnsureOfficial(order.Currency);
        var idempotencyKey = $"delivery-upgrade:{order.Id:N}:{payment.Id:N}";
        var metadata = new Dictionary<string, string>
        {
            ["kind"] = "delivery_upgrade",
            ["originalOrderId"] = order.Id.ToString(),
            ["order_id"] = order.Id.ToString(),
            ["payment_id"] = payment.Id.ToString(),
            ["customer_address_id"] = customerAddressId.ToString(),
            ["reason"] = reason.ToString(),
            ["delivery_fee"] = deliveryQuote.TotalFee.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["base_delivery_fee"] = deliveryQuote.BaseFee.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["distance_delivery_fee"] = deliveryQuote.DistanceFee.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["surge_delivery_fee"] = deliveryQuote.SurgeFee.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["changed_by_user_id"] = changedByUserId?.ToString() ?? string.Empty,
        };

        var session = await gateway.CreateSessionAsync(
            new CreatePaymentSessionCommand(
                OrderId: order.Id,
                PaymentId: payment.Id,
                Channel: PaymentMethodChannel.Card,
                Amount: delta,
                Currency: order.Currency,
                Description: $"Delivery upgrade for order {order.OrderNumber}",
                CallbackUrl: string.Empty,
                IdempotencyKey: idempotencyKey,
                Metadata: metadata,
                CustomerEmail: user.Email,
                CustomerPhone: user.PhoneNumber,
                CustomerFullName: user.FullName),
            cancellationToken);

        payment.ApplyProviderSession(
            providerName: session.ProviderName,
            providerMethod: "creditcard",
            providerPaymentId: session.ProviderPaymentId,
            providerInvoiceId: session.ProviderInvoiceId,
            idempotencyKey: idempotencyKey,
            rawCreateResponse: session.RawCreateResponse,
            currency: order.Currency);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ConvertOrderToDeliveryResultDto(
            order.Id,
            Converted: false,
            PaymentSessionUrl: session.ClientAction,
            PaymentId: payment.Id,
            Status: order.Status.ToString(),
            Message: "Delivery upgrade payment required before conversion can complete.");
    }

    private async Task<ConvertOrderToDeliveryResultDto> CompleteConversionAsync(
        Order order,
        Guid customerAddressId,
        ConvertToDeliveryReason reason,
        DeliveryPriceQuote deliveryQuote,
        Guid? changedByUserId,
        CancellationToken cancellationToken)
    {
        if (order.ConvertedToDeliveryAtUtc.HasValue)
        {
            return new ConvertOrderToDeliveryResultDto(
                order.Id,
                Converted: true,
                PaymentSessionUrl: null,
                PaymentId: null,
                Status: order.Status.ToString(),
                Message: "Order was already converted to delivery.");
        }

        var oldStatus = order.Status;
        order.ConvertToDelivery(
            customerAddressId,
            deliveryQuote.TotalFee,
            deliveryQuote.BaseFee,
            deliveryQuote.DistanceFee,
            deliveryQuote.SurgeFee,
            changedByUserId,
            reason);

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
                    ActorRole: ResolveActorRole(changedByUserId, order.VendorId)),
                cancellationToken);
        }

        await _deliveryDispatchService.TryAutoDispatchAsync(order.Id, cancellationToken: cancellationToken);

        return new ConvertOrderToDeliveryResultDto(
            order.Id,
            Converted: true,
            PaymentSessionUrl: null,
            PaymentId: null,
            Status: order.Status.ToString(),
            Message: "Order converted to delivery successfully.");
    }

    private static string ResolveActorRole(Guid? actorUserId, Guid vendorId) =>
        actorUserId == vendorId ? "vendor" : "admin";
}
