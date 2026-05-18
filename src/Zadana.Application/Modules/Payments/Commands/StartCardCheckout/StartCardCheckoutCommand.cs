using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Checkout.Support;
using Zadana.Application.Modules.Orders.Commands.PlaceOrder;
using Zadana.Application.Modules.Payments.DTOs;
using Zadana.Application.Modules.Payments.Gateways;
using Zadana.Application.Modules.Payments.Interfaces;
using Zadana.Application.Modules.Payments.Support;
using Zadana.Domain.Modules.Payments.Entities;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Finance;

namespace Zadana.Application.Modules.Payments.Commands.StartCardCheckout;

public record StartCardCheckoutCommand(
    Guid UserId,
    Guid VendorId,
    Guid CustomerAddressId,
    string PaymentMethodId,
    string? Notes,
    Guid? VendorBranchId,
    string? PromoCode) : IRequest<CardCheckoutResponseDto>;

public class StartCardCheckoutCommandValidator : AbstractValidator<StartCardCheckoutCommand>
{
    public StartCardCheckoutCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.VendorId).NotEmpty();
        RuleFor(x => x.CustomerAddressId).NotEmpty();
        RuleFor(x => x.PaymentMethodId).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.PromoCode).MaximumLength(100);
    }
}

public class StartCardCheckoutCommandHandler : IRequestHandler<StartCardCheckoutCommand, CardCheckoutResponseDto>
{
    public const string ProviderName = "Moyasar";

    private readonly IApplicationDbContext _context;
    private readonly IPaymentGatewayResolver _gatewayResolver;
    private readonly ISender _sender;
    private readonly IUnitOfWork _unitOfWork;

    public StartCardCheckoutCommandHandler(
        IApplicationDbContext context,
        IPaymentGatewayResolver gatewayResolver,
        ISender sender,
        IUnitOfWork unitOfWork)
    {
        _context = context;
        _gatewayResolver = gatewayResolver;
        _sender = sender;
        _unitOfWork = unitOfWork;
    }

    public async Task<CardCheckoutResponseDto> Handle(StartCardCheckoutCommand request, CancellationToken cancellationToken)
    {
        if (!_gatewayResolver.TryResolve(ProviderName, out var gateway) || gateway is null)
        {
            throw new BusinessRuleException("PAYMENT_UNAVAILABLE", "Card checkout provider is disabled or not configured.");
        }

        var channel = ResolveChannel(request.PaymentMethodId);
        var couponId = await ResolveCouponIdAsync(request.UserId, request.PromoCode, request.VendorId, cancellationToken);

        var cart = await _context.Carts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == request.UserId, cancellationToken)
            ?? throw new BusinessRuleException("EMPTY_CART", "Cart not found for checkout.");

        var address = await _context.CustomerAddresses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.CustomerAddressId && x.UserId == request.UserId, cancellationToken)
            ?? throw new NotFoundException("CustomerAddress", request.CustomerAddressId);

        var financeBreakdown = await CheckoutSupport.ResolveFinanceBreakdownV2Async(
            _context,
            address,
            cart.Subtotal,
            cart.DeliveryFee,
            cart.DiscountTotal,
            request.PaymentMethodId,
            cancellationToken);

        var orderId = await _sender.Send(
            new PlaceOrderCommand(
                UserId: request.UserId,
                VendorId: request.VendorId,
                CustomerAddressId: request.CustomerAddressId,
                PaymentMethod: nameof(PaymentMethodType.Card),
                Notes: request.Notes,
                VendorBranchId: request.VendorBranchId,
                CouponId: couponId,
                BaseDeliveryFee: cart.BaseDeliveryFee,
                DistanceDeliveryFee: cart.DistanceDeliveryFee,
                SurgeDeliveryFee: cart.SurgeDeliveryFee,
                QuotedDistanceKm: cart.QuotedDistanceKm,
                DeliveryPricingMode: cart.DeliveryPricingMode,
                DeliveryPricingRuleLabel: cart.DeliveryPricingRuleLabel,
                DriverToVendorDistanceKm: cart.DriverToVendorDistanceKm,
                VendorToCustomerDistanceKm: cart.VendorToCustomerDistanceKm,
                DriverToVendorFee: cart.DriverToVendorFee,
                VendorToCustomerFee: cart.VendorToCustomerFee,
                DriverToVendorPricingSource: cart.DriverToVendorPricingSource,
                VendorToCustomerPricingSource: cart.VendorToCustomerPricingSource,
                UsedEstimatedDriverPricing: cart.UsedEstimatedDriverPricing,
                PricingOriginType: cart.PricingOriginType,
                PricingOriginDriverId: cart.PricingOriginDriverId,
                DeliveryQuoteStatus: cart.DeliveryQuoteStatus,
                DeliveryQuoteLockedAtUtc: cart.DeliveryQuoteLockedAtUtc,
                DeliveryQuoteVersion: cart.DeliveryQuoteVersion,
                HasDeliveryAnomalyWarning: cart.HasDeliveryAnomalyWarning,
                VatAmount: financeBreakdown.VatAmount,
                CodFee: financeBreakdown.CodFee,
                ClearCartAfterPlacement: false),
            cancellationToken);

        var order = await _context.Orders
            .AsTracking()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken)
            ?? throw new NotFoundException("Order", orderId);

        CurrencyPolicy.EnsureOfficial(order.Currency);

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.UserId, cancellationToken)
            ?? throw new NotFoundException("User", request.UserId);

        var payment = new Payment(order.Id, PaymentMethodType.Card, order.TotalAmount);
        _context.Payments.Add(payment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            var idempotencyKey = $"payment-create:{order.Id:N}:{payment.Id:N}";
            var session = await gateway.CreateSessionAsync(
                new CreatePaymentSessionCommand(
                    OrderId: order.Id,
                    PaymentId: payment.Id,
                    Channel: channel,
                    Amount: order.TotalAmount,
                    Currency: order.Currency,
                    Description: $"Order {order.OrderNumber}",
                    CallbackUrl: string.Empty,
                    IdempotencyKey: idempotencyKey,
                    Metadata: new Dictionary<string, string>
                    {
                        ["order_id"] = order.Id.ToString(),
                        ["payment_id"] = payment.Id.ToString(),
                        ["order_number"] = order.OrderNumber,
                    },
                    CustomerEmail: user.Email,
                    CustomerPhone: user.PhoneNumber,
                    CustomerFullName: user.FullName),
                cancellationToken);

            payment.ApplyProviderSession(
                providerName: session.ProviderName,
                providerMethod: ChannelToProviderMethod(channel),
                providerPaymentId: session.ProviderPaymentId,
                providerInvoiceId: session.ProviderInvoiceId,
                idempotencyKey: idempotencyKey,
                rawCreateResponse: session.RawCreateResponse,
                currency: order.Currency);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new CardCheckoutResponseDto(
                LocalizedMessages.GetAr(LocalizedMessages.OrderPlacedSuccess),
                LocalizedMessages.GetEn(LocalizedMessages.OrderPlacedSuccess),
                new CardCheckoutOrderDto(
                    order.Id,
                    ToApiToken(order.Status.ToString()),
                    order.TotalAmount,
                    request.PaymentMethodId.ToLowerInvariant()),
                new CardCheckoutPaymentDto(
                    payment.Id,
                    session.ProviderName.ToLowerInvariant(),
                    ToApiToken(payment.Status.ToString()),
                    session.ClientAction,
                    session.ProviderConfig,
                    session.ProviderPaymentId));
        }
        catch
        {
            await UnconfirmedCardPaymentCleanup.DeleteOrderAsync(_context, order.Id, cancellationToken);
            throw;
        }
    }

    private async Task<Guid?> ResolveCouponIdAsync(Guid userId, string? promoCode, Guid vendorId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(promoCode))
        {
            return null;
        }

        var cart = await _context.Carts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken)
            ?? throw new BusinessRuleException("EMPTY_CART", "Cart not found for checkout.");

        var coupon = await CheckoutSupport.ResolveCouponByCodeAsync(
            _context,
            userId,
            promoCode,
            vendorId,
            cart.Subtotal,
            cancellationToken);

        return coupon.Id;
    }

    private static PaymentMethodChannel ResolveChannel(string paymentMethodId)
    {
        return paymentMethodId.Trim().ToLowerInvariant() switch
        {
            "card" or "credit_card" or "creditcard" or "debit_card" or "debitcard" => PaymentMethodChannel.Card,
            "apple_pay" or "applepay" => PaymentMethodChannel.ApplePay,
            "samsung_pay" or "samsungpay" => PaymentMethodChannel.SamsungPay,
            "stc_pay" or "stcpay" => PaymentMethodChannel.StcPay,
            _ => throw new BusinessRuleException("PAYMENT_METHOD_NOT_SUPPORTED", "Selected payment method is not supported by the card checkout flow."),
        };
    }

    private static string ChannelToProviderMethod(PaymentMethodChannel channel) => channel switch
    {
        PaymentMethodChannel.Card => "creditcard",
        PaymentMethodChannel.ApplePay => "applepay",
        PaymentMethodChannel.SamsungPay => "samsungpay",
        PaymentMethodChannel.StcPay => "stcpay",
        _ => "creditcard",
    };

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
