using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Checkout.DTOs;
using Zadana.Application.Modules.Checkout.Support;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Orders.Support;
using Zadana.Application.Modules.Payments.Interfaces;
using Zadana.Domain.Modules.Orders.Enums;

namespace Zadana.Application.Modules.Checkout.Queries.GetCheckoutSummary;

public record GetCheckoutSummaryQuery(
    Guid UserId,
    Guid? VendorId,
    Guid? AddressId,
    string? DeliverySlotId,
    string? PaymentMethod) : IRequest<CheckoutSummaryDto>;

public class GetCheckoutSummaryQueryHandler : IRequestHandler<GetCheckoutSummaryQuery, CheckoutSummaryDto>
{
    private const string CardProvider = "Moyasar";

    private readonly IApplicationDbContext _context;
    private readonly IPaymentGatewayResolver _gatewayResolver;
    private readonly IDeliveryPricingService _deliveryPricingService;

    public GetCheckoutSummaryQueryHandler(
        IApplicationDbContext context,
        IPaymentGatewayResolver gatewayResolver,
        IDeliveryPricingService deliveryPricingService)
    {
        _context = context;
        _gatewayResolver = gatewayResolver;
        _deliveryPricingService = deliveryPricingService;
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
        var coupon = await CheckoutSupport.ResolveAppliedCouponAsync(_context, request.UserId, cart, cancellationToken);
        var deliveryAssessment = await CheckoutSupport.EvaluateDeliveryAsync(
            _context,
            _deliveryPricingService,
            pricing.VendorBranchId,
            address,
            cancellationToken);
        var deliveryQuote = deliveryAssessment.DeliveryCheck.IsDeliverable
            ? deliveryAssessment.DeliveryQuote
            : CheckoutSupport.BuildNoPricingQuote();
        var preparationTimeMinutes = await _context.Vendors
            .AsNoTracking()
            .Where(v => v.Id == pricing.VendorId)
            .Select(v => v.PreparationTimeMinutes)
            .FirstOrDefaultAsync(cancellationToken);
        var operationalProfile = await DeliveryEtaTelemetry.LoadOperationalProfileAsync(
            _context,
            pricing.VendorId,
            pricing.VendorBranchId,
            address?.City,
            address?.Area,
            cancellationToken);
        var liveSignal = await DeliveryEtaTelemetry.LoadLiveSignalAsync(
            _context,
            pricing.VendorBranchId,
            cancellationToken);
        var estimatedDeliveryWindow = DeliveryEtaPolicy.EstimateCheckoutWindow(
            preparationTimeMinutes,
            deliveryAssessment.DeliveryCheck.IsDeliverable ? deliveryAssessment.DeliveryQuote.DriverToVendorDistanceKm : 0m,
            deliveryAssessment.DeliveryCheck.IsDeliverable ? deliveryAssessment.DeliveryQuote.VendorToCustomerDistanceKm : 0m,
            operationalProfile,
            liveSignal);
        var discount = coupon == null ? 0m : CheckoutSupport.CalculateDiscountAmount(coupon, pricing.Subtotal);
        var financeBreakdown = await CheckoutSupport.ResolveFinanceBreakdownV2Async(
            _context,
            address,
            pricing.Subtotal,
            deliveryQuote.TotalFee,
            discount,
            request.PaymentMethod,
            cancellationToken);

        // Fetch all customer addresses for address selection in checkout
        var allAddresses = await _context.CustomerAddresses
            .AsNoTracking()
            .Where(a => a.UserId == request.UserId)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.UpdatedAtUtc)
            .ThenByDescending(a => a.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return new CheckoutSummaryDto(
            new CheckoutCartDto(pricing.Items.Count, pricing.Items.Sum(x => x.Quantity), pricing.Items),
            CheckoutSupport.BuildAddressDto(address),
            CheckoutSupport.BuildAvailableAddressesList(allAddresses),
            CheckoutSupport.BuildDeliverySlots(request.DeliverySlotId),
            CheckoutSupport.BuildPaymentMethods(_gatewayResolver.TryResolve(CardProvider, out var gateway) && gateway is not null && gateway.IsEnabled),
            CheckoutSupport.BuildPromoCodeDto(coupon, discount),
            deliveryAssessment.DeliveryCheck,
            new CheckoutEstimatedDeliveryWindowDto(
                estimatedDeliveryWindow.MinMinutes,
                estimatedDeliveryWindow.MaxMinutes,
                estimatedDeliveryWindow.Confidence,
                estimatedDeliveryWindow.Source,
                estimatedDeliveryWindow.IsApproximate,
                estimatedDeliveryWindow.CalculationMode,
                estimatedDeliveryWindow.Explanation),
            CheckoutSupport.BuildDeliveryQuoteDto(deliveryQuote),
            CheckoutSupport.BuildDeliveryBreakdownDto(deliveryQuote),
            CheckoutSupport.BuildShippingBreakdownV2(deliveryQuote, financeBreakdown),
            deliveryQuote.PricingMode,
            financeBreakdown.Totals);
    }
}
