using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Checkout.DTOs;
using Zadana.Application.Modules.Checkout.Support;

namespace Zadana.Application.Modules.Checkout.Commands.RemoveCheckoutPromoCode;

public record RemoveCheckoutPromoCodeCommand(Guid UserId, Guid? VendorId) : IRequest<RemoveCheckoutPromoCodeResultDto>;

public class RemoveCheckoutPromoCodeCommandHandler : IRequestHandler<RemoveCheckoutPromoCodeCommand, RemoveCheckoutPromoCodeResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public RemoveCheckoutPromoCodeCommandHandler(IApplicationDbContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<RemoveCheckoutPromoCodeResultDto> Handle(RemoveCheckoutPromoCodeCommand request, CancellationToken cancellationToken)
    {
        var cart = await CheckoutSupport.GetRequiredCartAsync(_context, request.UserId, cancellationToken, asTracking: true);
        var pricing = await CheckoutSupport.BuildPricingSnapshotAsync(_context, cart, request.VendorId, cancellationToken);
        var shippingCost = CheckoutSupport.ResolveShippingCost(cart);

        cart.UpdateTotals(pricing.Subtotal, shippingCost);
        cart.RemoveCoupon();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new RemoveCheckoutPromoCodeResultDto(
            "promo code removed successfully",
            CheckoutSupport.BuildTotals(pricing.Subtotal, shippingCost, 0m));
    }
}
