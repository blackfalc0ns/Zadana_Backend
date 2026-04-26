using FluentValidation;
using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Checkout.DTOs;
using Zadana.Application.Modules.Checkout.Support;
using Zadana.Application.Modules.Delivery.Interfaces;

namespace Zadana.Application.Modules.Checkout.Commands.ApplyCheckoutPromoCode;

public record ApplyCheckoutPromoCodeCommand(Guid UserId, Guid? VendorId, string Code) : IRequest<ApplyCheckoutPromoCodeResultDto>;

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
    private readonly IDeliveryPricingService _deliveryPricingService;

    public ApplyCheckoutPromoCodeCommandHandler(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IDeliveryPricingService deliveryPricingService)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _deliveryPricingService = deliveryPricingService;
    }

    public async Task<ApplyCheckoutPromoCodeResultDto> Handle(ApplyCheckoutPromoCodeCommand request, CancellationToken cancellationToken)
    {
        var cart = await CheckoutSupport.GetRequiredCartAsync(_context, request.UserId, cancellationToken, asTracking: true);
        var pricing = await CheckoutSupport.BuildPricingSnapshotAsync(_context, cart, request.VendorId, cancellationToken);
        var address = await CheckoutSupport.ResolveSelectedAddressAsync(_context, request.UserId, null, cancellationToken);
        var coupon = await CheckoutSupport.ResolveCouponByCodeAsync(_context, request.Code, pricing.VendorId, pricing.Subtotal, cancellationToken);
        var deliveryQuote = await CheckoutSupport.QuoteDeliveryOrFallbackAsync(
            _deliveryPricingService,
            pricing.VendorBranchId,
            address,
            cancellationToken);
        var discount = CheckoutSupport.CalculateDiscountAmount(coupon, pricing.Subtotal);

        cart.UpdateTotals(
            pricing.Subtotal,
            deliveryQuote.TotalFee,
            deliveryQuote.BaseFee,
            deliveryQuote.DistanceFee,
            deliveryQuote.SurgeFee,
            deliveryQuote.DistanceKm,
            deliveryQuote.PricingMode,
            deliveryQuote.RuleLabel);
        cart.ApplyCoupon(coupon.Id, discount);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ApplyCheckoutPromoCodeResultDto(
            "promo code applied successfully",
            CheckoutSupport.BuildPromoCodeDto(coupon, discount)!,
            CheckoutSupport.BuildTotals(pricing.Subtotal, deliveryQuote.TotalFee, discount));
    }
}
