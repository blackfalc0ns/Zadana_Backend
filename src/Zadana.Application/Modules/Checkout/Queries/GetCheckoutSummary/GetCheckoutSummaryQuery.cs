using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Checkout.DTOs;
using Zadana.Application.Modules.Checkout.Support;
using Zadana.Application.Modules.Orders.Support;
using Zadana.Application.Modules.Payments.Interfaces;

namespace Zadana.Application.Modules.Checkout.Queries.GetCheckoutSummary;

public record GetCheckoutSummaryQuery(Guid UserId, Guid? VendorId, Guid? AddressId, string? DeliverySlotId) : IRequest<CheckoutSummaryDto>;

public class GetCheckoutSummaryQueryHandler : IRequestHandler<GetCheckoutSummaryQuery, CheckoutSummaryDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IPaymobGateway _paymobGateway;

    public GetCheckoutSummaryQueryHandler(IApplicationDbContext context, IPaymobGateway paymobGateway)
    {
        _context = context;
        _paymobGateway = paymobGateway;
    }

    public async Task<CheckoutSummaryDto> Handle(GetCheckoutSummaryQuery request, CancellationToken cancellationToken)
    {
        await CartCleanupSupport.ClearStalePaidCheckoutCartIfNeededAsync(
            _context,
            request.UserId,
            null,
            cancellationToken);

        var cart = await CheckoutSupport.GetRequiredCartAsync(_context, request.UserId, cancellationToken);
        var pricing = await CheckoutSupport.BuildPricingSnapshotAsync(_context, cart, request.VendorId, cancellationToken);
        var address = await CheckoutSupport.ResolveSelectedAddressAsync(_context, request.UserId, request.AddressId, cancellationToken);
        var coupon = await CheckoutSupport.ResolveAppliedCouponAsync(_context, cart, cancellationToken);
        var shippingCost = CheckoutSupport.ResolveShippingCost(cart);
        var discount = coupon == null ? 0m : CheckoutSupport.CalculateDiscountAmount(coupon, pricing.Subtotal);

        return new CheckoutSummaryDto(
            new CheckoutCartDto(pricing.Items.Count, pricing.Items.Sum(x => x.Quantity), pricing.Items),
            CheckoutSupport.BuildAddressDto(address),
            CheckoutSupport.BuildDeliverySlots(request.DeliverySlotId),
            CheckoutSupport.BuildPaymentMethods(_paymobGateway.IsEnabled),
            CheckoutSupport.BuildPromoCodeDto(coupon, discount),
            CheckoutSupport.BuildTotals(pricing.Subtotal, shippingCost, discount));
    }
}
