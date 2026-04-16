using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Orders.Commands.PlaceOrder;
using Zadana.Application.Modules.Payments.DTOs;
using Zadana.Application.Modules.Payments.Interfaces;
using Zadana.Domain.Modules.Payments.Entities;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Payments.Commands.StartPaymobCheckout;

public class StartPaymobCheckoutCommandHandler : IRequestHandler<StartPaymobCheckoutCommand, PaymobCheckoutResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IPaymobGateway _paymobGateway;
    private readonly ISender _sender;
    private readonly IUnitOfWork _unitOfWork;

    public StartPaymobCheckoutCommandHandler(
        IApplicationDbContext context,
        IPaymobGateway paymobGateway,
        ISender sender,
        IUnitOfWork unitOfWork)
    {
        _context = context;
        _paymobGateway = paymobGateway;
        _sender = sender;
        _unitOfWork = unitOfWork;
    }

    public async Task<PaymobCheckoutResponseDto> Handle(StartPaymobCheckoutCommand request, CancellationToken cancellationToken)
    {
        if (!_paymobGateway.IsEnabled)
        {
            throw new BusinessRuleException("PAYMENT_UNAVAILABLE", "Paymob checkout is disabled or not configured.");
        }

        if (!string.Equals(request.PaymentMethodId, "card", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException("PAYMENT_METHOD_NOT_SUPPORTED", "Only card payments are supported in this checkout flow.");
        }

        var couponId = await ResolveCouponIdAsync(request.PromoCode, request.VendorId, cancellationToken);

        var orderId = await _sender.Send(
            new PlaceOrderCommand(
                request.UserId,
                request.VendorId,
                request.CustomerAddressId,
                nameof(PaymentMethodType.Card),
                request.Notes,
                request.VendorBranchId,
                couponId,
                false),
            cancellationToken);

        var order = await _context.Orders
            .AsTracking()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken)
            ?? throw new NotFoundException("Order", orderId);

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.UserId, cancellationToken)
            ?? throw new NotFoundException("User", request.UserId);

        var address = await _context.CustomerAddresses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.CustomerAddressId && x.UserId == request.UserId, cancellationToken)
            ?? throw new NotFoundException("CustomerAddress", request.CustomerAddressId);

        var payment = new Payment(order.Id, PaymentMethodType.Card, order.TotalAmount);
        _context.Payments.Add(payment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            var session = await _paymobGateway.CreateCheckoutSessionAsync(
                new PaymobCheckoutSessionRequest(
                    payment.Id,
                    order.Id,
                    order.OrderNumber,
                    order.TotalAmount,
                    "EGP",
                    order.Items.Select(MapPaymobItem).ToArray(),
                    GetFirstName(user.FullName),
                    GetLastName(user.FullName),
                    user.Email ?? string.Empty,
                    user.PhoneNumber ?? address.ContactPhone,
                    address.AddressLine,
                    address.City ?? address.Area ?? "Cairo",
                    "EG"),
                cancellationToken);

            payment.MarkAsPending("Paymob", session.ProviderReference);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new PaymobCheckoutResponseDto(
                "order placed successfully",
                new PaymobCheckoutOrderDto(
                    order.Id,
                    ToApiToken(order.Status.ToString()),
                    order.TotalAmount,
                    request.PaymentMethodId.ToLowerInvariant()),
                new PaymobCheckoutPaymentDto(
                    payment.Id,
                    "paymob",
                    ToApiToken(payment.Status.ToString()),
                    session.IframeUrl,
                    session.ProviderReference));
        }
        catch
        {
            payment.MarkAsFailed("Paymob checkout session creation failed.");
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    private async Task<Guid?> ResolveCouponIdAsync(string? promoCode, Guid vendorId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(promoCode))
        {
            return null;
        }

        var normalizedCode = promoCode.Trim().ToUpperInvariant();
        var coupon = await _context.Coupons
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Code == normalizedCode, cancellationToken);

        if (coupon == null || !coupon.IsValid())
        {
            throw new BusinessRuleException("INVALID_PROMO_CODE", "Promo code is invalid or inactive.");
        }

        var hasVendorRestrictions = await _context.CouponVendors
            .AsNoTracking()
            .AnyAsync(x => x.CouponId == coupon.Id, cancellationToken);

        if (hasVendorRestrictions)
        {
            var isApplicable = await _context.CouponVendors
                .AsNoTracking()
                .AnyAsync(x => x.CouponId == coupon.Id && x.VendorId == vendorId, cancellationToken);

            if (!isApplicable)
            {
                throw new BusinessRuleException("PROMO_CODE_NOT_APPLICABLE", "Promo code is not applicable to the selected vendor.");
            }
        }

        return coupon.Id;
    }

    private static string GetFirstName(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.FirstOrDefault() ?? "Customer";
    }

    private static string GetLastName(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length > 1 ? string.Join(' ', parts.Skip(1)) : "Customer";
    }

    private static PaymobOrderItemRequest MapPaymobItem(Zadana.Domain.Modules.Orders.Entities.OrderItem item) =>
        new(
            item.ProductName,
            item.ProductName,
            item.Quantity,
            item.UnitPrice);

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
