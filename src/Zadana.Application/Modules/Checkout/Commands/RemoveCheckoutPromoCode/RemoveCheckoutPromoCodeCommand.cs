using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Checkout.DTOs;
using Zadana.Application.Modules.Checkout.Support;
using Zadana.Application.Modules.Delivery.Interfaces;

namespace Zadana.Application.Modules.Checkout.Commands.RemoveCheckoutPromoCode;

public record RemoveCheckoutPromoCodeCommand(Guid UserId, Guid? VendorId) : IRequest<RemoveCheckoutPromoCodeResultDto>;

public class RemoveCheckoutPromoCodeCommandHandler : IRequestHandler<RemoveCheckoutPromoCodeCommand, RemoveCheckoutPromoCodeResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDeliveryPricingService _deliveryPricingService;

    public RemoveCheckoutPromoCodeCommandHandler(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IDeliveryPricingService deliveryPricingService)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _deliveryPricingService = deliveryPricingService;
    }

    public async Task<RemoveCheckoutPromoCodeResultDto> Handle(RemoveCheckoutPromoCodeCommand request, CancellationToken cancellationToken)
    {
        var cart = await CheckoutSupport.GetRequiredCartAsync(_context, request.UserId, cancellationToken, asTracking: true);
        var pricing = await CheckoutSupport.BuildPricingSnapshotAsync(_context, cart, request.VendorId, cancellationToken);
        var address = await CheckoutSupport.ResolveSelectedAddressAsync(_context, request.UserId, null, cancellationToken);
        var deliveryQuote = pricing.VendorBranchId.HasValue && address is not null
            ? await _deliveryPricingService.QuoteAsync(pricing.VendorBranchId.Value, address.Id, cancellationToken)
            : new DeliveryPriceQuote(0m, 0m, 0m, 0m, 0m, "zone-fallback", "No pricing");

        cart.UpdateTotals(
            pricing.Subtotal,
            deliveryQuote.TotalFee,
            deliveryQuote.BaseFee,
            deliveryQuote.DistanceFee,
            deliveryQuote.SurgeFee,
            deliveryQuote.DistanceKm,
            deliveryQuote.PricingMode,
            deliveryQuote.RuleLabel);
        cart.RemoveCoupon();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new RemoveCheckoutPromoCodeResultDto(
            "promo code removed successfully",
            CheckoutSupport.BuildTotals(pricing.Subtotal, deliveryQuote.TotalFee, 0m));
    }
}
