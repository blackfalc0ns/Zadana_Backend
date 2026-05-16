using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Checkout.DTOs;
using Zadana.Application.Modules.Checkout.Support;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Orders.Commands.PlaceOrder;
using Zadana.Application.Modules.Orders.Events;
using Zadana.Application.Modules.Orders.Support;
using Zadana.Application.Modules.Payments.DTOs;
using Zadana.Application.Modules.Payments.Interfaces;
using Zadana.Application.Modules.Payments.Support;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Entities;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Checkout.Commands.PlaceCheckoutOrder;

public record PlaceCheckoutOrderCommand(
    Guid UserId,
    Guid? VendorId,
    Guid? AddressId,
    string? DeliverySlotId,
    string PaymentMethod,
    string? PromoCode,
    string? Notes,
    string? DeviceId = null) : IRequest<PlaceCheckoutOrderResultDto>;

public class PlaceCheckoutOrderCommandValidator : AbstractValidator<PlaceCheckoutOrderCommand>
{
    public PlaceCheckoutOrderCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.PaymentMethod).NotEmpty();
        RuleFor(x => x.PromoCode).MaximumLength(100);
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}

public class PlaceCheckoutOrderCommandHandler : IRequestHandler<PlaceCheckoutOrderCommand, PlaceCheckoutOrderResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IPaymobGateway _paymobGateway;
    private readonly IDeliveryPricingService _deliveryPricingService;
    private readonly ISender _sender;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublisher _publisher;

    public PlaceCheckoutOrderCommandHandler(
        IApplicationDbContext context,
        IPaymobGateway paymobGateway,
        IDeliveryPricingService deliveryPricingService,
        ISender sender,
        IUnitOfWork unitOfWork,
        IPublisher publisher)
    {
        _context = context;
        _paymobGateway = paymobGateway;
        _deliveryPricingService = deliveryPricingService;
        _sender = sender;
        _unitOfWork = unitOfWork;
        _publisher = publisher;
    }

    public async Task<PlaceCheckoutOrderResultDto> Handle(PlaceCheckoutOrderCommand request, CancellationToken cancellationToken)
    {
        ValidateDeliverySlot(request.DeliverySlotId);

        var paymentMethodCode = CheckoutSupport.NormalizePaymentMethodCode(request.PaymentMethod)
            ?? throw new BusinessRuleException("PAYMENT_METHOD_NOT_SUPPORTED", "Selected payment method is not supported.");
        if (paymentMethodCode == "apple_pay")
        {
            throw new BusinessRuleException("PAYMENT_METHOD_NOT_SUPPORTED", "Apple Pay is not available yet.");
        }

        var cart = await CheckoutSupport.GetRequiredCartAsync(_context, request.UserId, cancellationToken, asTracking: true);
        var pricing = await CheckoutSupport.BuildPricingSnapshotAsync(_context, cart, request.VendorId, cancellationToken);
        var address = await CheckoutSupport.ResolveSelectedAddressAsync(_context, request.UserId, request.AddressId, cancellationToken);
        if (address is null)
        {
            if (request.AddressId.HasValue)
            {
                throw new NotFoundException("CustomerAddress", request.AddressId.Value);
            }

            throw new BusinessRuleException("CUSTOMER_ADDRESS_REQUIRED", "Customer must have a default address before placing an order.");
        }

        var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == request.UserId, cancellationToken)
            ?? throw new NotFoundException("User", request.UserId);

        var deliveryAssessment = await CheckoutSupport.EvaluateDeliveryAsync(
            _context,
            _deliveryPricingService,
            pricing.VendorBranchId,
            address,
            cancellationToken);
        if (!deliveryAssessment.DeliveryCheck.CanProceedToCheckout)
        {
            throw deliveryAssessment.DeliveryCheck.Status switch
            {
                "address_required" => new BusinessRuleException("CUSTOMER_ADDRESS_REQUIRED", deliveryAssessment.DeliveryCheck.MessageEn),
                "undeliverable" => new BusinessRuleException("DELIVERY_NOT_AVAILABLE", deliveryAssessment.DeliveryCheck.MessageEn),
                _ => new BusinessRuleException("DELIVERY_PRICING_UNAVAILABLE", deliveryAssessment.DeliveryCheck.MessageEn)
            };
        }

        var deliveryQuote = deliveryAssessment.DeliveryQuote;
        var coupon = await ResolveOrderCouponAsync(request.UserId, cart, request.PromoCode, pricing.VendorId, pricing.Subtotal, cancellationToken);
        var discount = coupon == null ? 0m : CheckoutSupport.CalculateDiscountAmount(coupon, pricing.Subtotal);
        var financeBreakdown = await CheckoutSupport.ResolveFinanceBreakdownV2Async(
            _context,
            address,
            pricing.Subtotal,
            deliveryQuote.TotalFee,
            discount,
            paymentMethodCode,
            cancellationToken);

        cart.UpdateTotals(
            pricing.Subtotal,
            deliveryQuote.TotalFee,
            deliveryQuote.BaseFee,
            deliveryQuote.DistanceFee,
            deliveryQuote.SurgeFee,
            deliveryQuote.DistanceKm,
            deliveryQuote.PricingMode,
            deliveryQuote.RuleLabel,
            deliveryQuote.DriverToVendorDistanceKm,
            deliveryQuote.VendorToCustomerDistanceKm,
            deliveryQuote.DriverToVendorFee,
            deliveryQuote.VendorToCustomerFee,
            deliveryQuote.DriverToVendorPricingSource,
            deliveryQuote.VendorToCustomerPricingSource,
            deliveryQuote.UsedEstimatedDriverPricing,
            deliveryQuote.PricingOriginType,
            deliveryQuote.PricingOriginDriverId,
            deliveryQuote.DeliveryQuoteStatus,
            deliveryQuote.QuoteLockedAtUtc,
            deliveryQuote.QuoteVersion,
            deliveryQuote.HasAnomalyWarning);
        if (coupon == null)
        {
            cart.RemoveCoupon();
        }
        else
        {
            cart.ApplyCoupon(coupon.Id, discount);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var internalPaymentMethod = CheckoutSupport.MapPaymentMethodCodeToEnumName(paymentMethodCode);
        var shouldClearCartAfterPlacement = paymentMethodCode is "cash" or "bank";
        var orderId = await _sender.Send(
            new PlaceOrderCommand(
                UserId: request.UserId,
                VendorId: pricing.VendorId,
                CustomerAddressId: address.Id,
                PaymentMethod: internalPaymentMethod,
                Notes: request.Notes,
                VendorBranchId: pricing.VendorBranchId,
                CouponId: coupon?.Id,
                BaseDeliveryFee: deliveryQuote.BaseFee,
                DistanceDeliveryFee: deliveryQuote.DistanceFee,
                SurgeDeliveryFee: deliveryQuote.SurgeFee,
                QuotedDistanceKm: deliveryQuote.DistanceKm,
                DeliveryPricingMode: deliveryQuote.PricingMode,
                DeliveryPricingRuleLabel: deliveryQuote.RuleLabel,
                DriverToVendorDistanceKm: deliveryQuote.DriverToVendorDistanceKm,
                VendorToCustomerDistanceKm: deliveryQuote.VendorToCustomerDistanceKm,
                DriverToVendorFee: deliveryQuote.DriverToVendorFee,
                VendorToCustomerFee: deliveryQuote.VendorToCustomerFee,
                DriverToVendorPricingSource: deliveryQuote.DriverToVendorPricingSource,
                VendorToCustomerPricingSource: deliveryQuote.VendorToCustomerPricingSource,
                UsedEstimatedDriverPricing: deliveryQuote.UsedEstimatedDriverPricing,
                PricingOriginType: deliveryQuote.PricingOriginType,
                PricingOriginDriverId: deliveryQuote.PricingOriginDriverId,
                DeliveryQuoteStatus: deliveryQuote.DeliveryQuoteStatus,
                DeliveryQuoteLockedAtUtc: deliveryQuote.QuoteLockedAtUtc,
                DeliveryQuoteVersion: deliveryQuote.QuoteVersion,
                HasDeliveryAnomalyWarning: deliveryQuote.HasAnomalyWarning,
                VatAmount: financeBreakdown.VatAmount,
                CodFee: financeBreakdown.CodFee,
                ClearCartAfterPlacement: shouldClearCartAfterPlacement),
            cancellationToken);

        var order = await _context.Orders
            .AsTracking()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken)
            ?? throw new NotFoundException("Order", orderId);

        var payment = new Payment(order.Id, Enum.Parse<PaymentMethodType>(internalPaymentMethod, true), order.TotalAmount);
        payment.SetCheckoutDeviceId(request.DeviceId);
        _context.Payments.Add(payment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        CheckoutPaymentSessionDto? paymentSession = null;

        if (paymentMethodCode == "card")
        {
            if (!_paymobGateway.IsEnabled)
            {
                throw new BusinessRuleException("PAYMENT_UNAVAILABLE", "Paymob checkout is disabled or not configured.");
            }

            try
            {
                var session = await _paymobGateway.CreateCheckoutSessionAsync(
                    new PaymobCheckoutSessionRequest(
                        payment.Id,
                        order.Id,
                        order.OrderNumber,
                        order.TotalAmount,
                        CheckoutSupport.Currency,
                        order.Items.Select(MapPaymobItem).ToArray(),
                        PaymobBillingData.FirstName(user),
                        PaymobBillingData.LastName(user),
                        PaymobBillingData.Email(user),
                        PaymobBillingData.Phone(user, address),
                        PaymobBillingData.Street(address),
                        PaymobBillingData.City(address),
                        "EG"),
                    cancellationToken);

                payment.MarkAsPending("Paymob", session.ProviderReference);
                paymentSession = new CheckoutPaymentSessionDto(
                    payment.Id,
                    "paymob",
                    CheckoutSupport.MapPaymentStatusToContractValue(payment.Status.ToString()),
                    session.IframeUrl,
                    session.ProviderReference);
            }
            catch
            {
                await UnconfirmedCardPaymentCleanup.DeleteOrderAsync(_context, order.Id, cancellationToken);
                throw;
            }
        }
        else if (paymentMethodCode == "cash")
        {
            payment.MarkAsPending("CashOnDelivery", $"COD-{order.OrderNumber}");
            order.ChangeStatus(OrderStatus.Placed, null, "Cash on delivery selected");
            order.ChangeStatus(OrderStatus.PendingVendorAcceptance, null, "Awaiting vendor response");
            OrderStatusHistoryTracking.TrackNewEntries(_context, order);
        }
        else if (paymentMethodCode == "bank")
        {
            payment.MarkAsPending("BankTransfer", $"BANK-{order.OrderNumber}");
            order.ChangeStatus(OrderStatus.Placed, null, "Bank transfer selected");
            order.ChangeStatus(OrderStatus.PendingVendorAcceptance, null, "Awaiting bank transfer confirmation");
            OrderStatusHistoryTracking.TrackNewEntries(_context, order);
        }
        else
        {
            throw new BusinessRuleException("PAYMENT_METHOD_NOT_SUPPORTED", "Selected payment method is not supported.");
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Publish notification for order placement
        if (paymentMethodCode is "cash" or "bank")
        {
            await _publisher.Publish(
                new OrderStatusChangedNotification(
                    order.Id,
                    request.UserId,
                    order.VendorId,
                    order.OrderNumber,
                    OrderStatus.PendingPayment,
                    order.Status,
                    NotifyCustomer: true,
                    NotifyVendor: true,
                    ActorRole: "customer"),
                cancellationToken);
        }

        return new PlaceCheckoutOrderResultDto(
            LocalizedMessages.GetAr(LocalizedMessages.OrderPlacedSuccess),
            LocalizedMessages.GetEn(LocalizedMessages.OrderPlacedSuccess),
            new CheckoutPlacedOrderDto(
                order.Id,
                order.PlacedAtUtc,
                CheckoutSupport.MapOrderStatusToContractValue(order.Status.ToString()),
                paymentMethodCode,
                CheckoutSupport.MapPaymentStatusToContractValue(order.PaymentStatus.ToString()),
                order.TotalAmount),
            paymentSession);
    }

    private async Task<Zadana.Domain.Modules.Marketing.Entities.Coupon?> ResolveOrderCouponAsync(
        Guid userId,
        Zadana.Domain.Modules.Orders.Entities.Cart cart,
        string? promoCode,
        Guid vendorId,
        decimal subtotal,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(promoCode))
        {
            return await CheckoutSupport.ResolveCouponByCodeAsync(_context, userId, promoCode, vendorId, subtotal, cancellationToken);
        }

        return await CheckoutSupport.ResolveAppliedCouponAsync(_context, userId, cart, cancellationToken);
    }

    private static void ValidateDeliverySlot(string? deliverySlotId)
    {
        if (!string.IsNullOrWhiteSpace(deliverySlotId) &&
            !deliverySlotId.Trim().Equals(CheckoutSupport.DefaultDeliverySlotId, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException("DELIVERY_SLOT_NOT_AVAILABLE", "Selected delivery slot is not available.");
        }
    }

    private static PaymobOrderItemRequest MapPaymobItem(Zadana.Domain.Modules.Orders.Entities.OrderItem item) =>
        new(
            item.ProductName,
            item.ProductName,
            item.Quantity,
            item.UnitPrice);
}
