using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Checkout.DTOs;
using Zadana.Application.Modules.Checkout.Support;
using Zadana.Application.Modules.Delivery.Interfaces;

namespace Zadana.Application.Modules.Checkout.Queries.GetCartDeliveryCheck;

public record GetCartDeliveryCheckQuery(
    Guid UserId,
    Guid? VendorId,
    Guid? AddressId) : IRequest<CartDeliveryCheckDto>;

public record CartDeliveryCheckDto(
    Guid? AddressId,
    CheckoutSelectedAddressDto? SelectedAddress,
    CheckoutDeliveryCheckDto DeliveryCheck,
    CheckoutDeliveryQuoteDto DeliveryQuote);

public class GetCartDeliveryCheckQueryHandler : IRequestHandler<GetCartDeliveryCheckQuery, CartDeliveryCheckDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IDeliveryPricingService _deliveryPricingService;

    public GetCartDeliveryCheckQueryHandler(
        IApplicationDbContext context,
        IDeliveryPricingService deliveryPricingService)
    {
        _context = context;
        _deliveryPricingService = deliveryPricingService;
    }

    public async Task<CartDeliveryCheckDto> Handle(GetCartDeliveryCheckQuery request, CancellationToken cancellationToken)
    {
        var cart = await CheckoutSupport.GetRequiredCartAsync(_context, request.UserId, cancellationToken);
        var pricing = await CheckoutSupport.BuildPricingSnapshotAsync(_context, cart, request.VendorId, cancellationToken);
        var address = await CheckoutSupport.ResolveSelectedAddressAsync(_context, request.UserId, request.AddressId, cancellationToken);
        var deliveryAssessment = await CheckoutSupport.EvaluateDeliveryAsync(
            _context,
            _deliveryPricingService,
            pricing.VendorBranchId,
            address,
            cancellationToken,
            pricing.Subtotal);

        var visibleQuote = deliveryAssessment.DeliveryCheck.IsDeliverable
            ? deliveryAssessment.DeliveryQuote
            : CheckoutSupport.BuildNoPricingQuote();

        return new CartDeliveryCheckDto(
            address?.Id,
            CheckoutSupport.BuildAddressDto(address),
            deliveryAssessment.DeliveryCheck,
            CheckoutSupport.BuildDeliveryQuoteDto(visibleQuote));
    }
}
