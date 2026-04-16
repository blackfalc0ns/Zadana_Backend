using FluentValidation;
using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Checkout.DTOs;
using Zadana.Application.Modules.Checkout.Support;

namespace Zadana.Application.Modules.Checkout.Commands.ApplyCheckoutPromoCode;

public record ApplyCheckoutPromoCodeCommand(Guid UserId, string Code) : IRequest<ApplyCheckoutPromoCodeResultDto>;

public class ApplyCheckoutPromoCodeCommandValidator : AbstractValidator<ApplyCheckoutPromoCodeCommand>
{
    public ApplyCheckoutPromoCodeCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(100);
    }
}

public class ApplyCheckoutPromoCodeCommandHandler : IRequestHandler<ApplyCheckoutPromoCodeCommand, ApplyCheckoutPromoCodeResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public ApplyCheckoutPromoCodeCommandHandler(IApplicationDbContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplyCheckoutPromoCodeResultDto> Handle(ApplyCheckoutPromoCodeCommand request, CancellationToken cancellationToken)
    {
        var cart = await CheckoutSupport.GetRequiredCartAsync(_context, request.UserId, cancellationToken, asTracking: true);
        var pricing = await CheckoutSupport.BuildPricingSnapshotAsync(_context, cart, cancellationToken);
        var coupon = await CheckoutSupport.ResolveCouponByCodeAsync(_context, request.Code, pricing.VendorId, pricing.Subtotal, cancellationToken);
        var shippingCost = CheckoutSupport.ResolveShippingCost(cart);
        var discount = CheckoutSupport.CalculateDiscountAmount(coupon, pricing.Subtotal);

        cart.UpdateTotals(pricing.Subtotal, shippingCost);
        cart.ApplyCoupon(coupon.Id, discount);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ApplyCheckoutPromoCodeResultDto(
            "promo code applied successfully",
            CheckoutSupport.BuildPromoCodeDto(coupon, discount)!,
            CheckoutSupport.BuildTotals(pricing.Subtotal, shippingCost, discount));
    }
}
