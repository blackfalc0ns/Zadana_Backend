using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Checkout.DTOs;
using Zadana.Application.Modules.Checkout.Support;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Orders.Support;
using Zadana.Application.Modules.Payments.Interfaces;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Checkout.Queries.GetCheckoutSummary;

public record GetCheckoutSummaryQuery(
    Guid UserId,
    Guid? VendorId,
    Guid? AddressId,
    string? DeliverySlotId,
    string? PaymentMethod,
    FulfillmentType Fulfillment = FulfillmentType.Delivery,
    Guid? VendorBranchId = null) : IRequest<CheckoutSummaryDto>;

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

        var pickupSettings = await CheckoutSupport.LoadPlatformPickupSettingsAsync(_context, cancellationToken);
        if (request.Fulfillment == FulfillmentType.Delivery && !pickupSettings.DeliveryOptionEnabled)
        {
            throw new BusinessRuleException("DELIVERY_DISABLED_BY_ADMIN", "Delivery checkout is currently disabled.");
        }

        if (request.Fulfillment == FulfillmentType.Pickup && !pickupSettings.PickupOptionEnabled)
        {
            throw new BusinessRuleException("PICKUP_DISABLED_BY_ADMIN", "Pickup checkout is currently disabled.");
        }

        var cart = await CheckoutSupport.GetRequiredCartAsync(_context, request.UserId, cancellationToken);
        var cardAvailable = _gatewayResolver.TryResolve(CardProvider, out var gateway) && gateway is not null && gateway.IsEnabled;
        var address = await CheckoutSupport.ResolveSelectedAddressAsync(_context, request.UserId, request.AddressId, cancellationToken);
        var pickupBranchId = request.Fulfillment == FulfillmentType.Pickup
            ? request.VendorBranchId
            : null;
        if (request.Fulfillment == FulfillmentType.Pickup &&
            !pickupBranchId.HasValue &&
            request.VendorId.HasValue)
        {
            pickupBranchId = await CheckoutSupport.ResolvePickupBranchForCartAsync(
                _context,
                cart,
                request.VendorId.Value,
                address,
                cancellationToken);
        }

        var pricing = await CheckoutSupport.BuildPricingSnapshotAsync(
            _context,
            cart,
            request.VendorId,
            request.Fulfillment == FulfillmentType.Pickup ? null : address,
            cancellationToken,
            pickupBranchId);
        if (request.Fulfillment == FulfillmentType.Pickup && !pickupBranchId.HasValue)
        {
            pickupBranchId = await CheckoutSupport.ResolvePickupBranchForCartAsync(
                _context,
                cart,
                pricing.VendorId,
                address,
                cancellationToken);

            if (pickupBranchId.HasValue)
            {
                pricing = await CheckoutSupport.BuildPricingSnapshotAsync(
                    _context,
                    cart,
                    pricing.VendorId,
                    null,
                    cancellationToken,
                    pickupBranchId);
            }
        }

        var coupon = await CheckoutSupport.ResolveAppliedCouponAsync(_context, request.UserId, cart, cancellationToken);
        var discount = coupon == null ? 0m : CheckoutSupport.CalculateDiscountAmount(coupon, pricing.Subtotal);

        if (request.Fulfillment == FulfillmentType.Pickup)
        {
            return await BuildPickupSummaryAsync(
                request,
                cart,
                pricing,
                pickupBranchId,
                address,
                coupon,
                discount,
                cardAvailable,
                pickupSettings.PickupCashOnPickupEnabled,
                cancellationToken);
        }

        var deliveryBranchId = await CheckoutSupport.ResolveDeliveryBranchIdAsync(_context, pricing, address, cancellationToken);
        var deliveryAssessment = await CheckoutSupport.EvaluateDeliveryAsync(
            _context,
            _deliveryPricingService,
            deliveryBranchId,
            address,
            cancellationToken,
            pricing.Subtotal);
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
            deliveryBranchId,
            address?.City,
            address?.Area,
            cancellationToken);
        var liveSignal = await DeliveryEtaTelemetry.LoadLiveSignalAsync(
            _context,
            deliveryBranchId,
            cancellationToken);
        var estimatedDeliveryWindow = DeliveryEtaPolicy.EstimateCheckoutWindow(
            preparationTimeMinutes,
            deliveryAssessment.DeliveryCheck.IsDeliverable ? deliveryAssessment.DeliveryQuote.DriverToVendorDistanceKm : 0m,
            deliveryAssessment.DeliveryCheck.IsDeliverable ? deliveryAssessment.DeliveryQuote.VendorToCustomerDistanceKm : 0m,
            operationalProfile,
            liveSignal);
        var financeBreakdown = await CheckoutSupport.ResolveFinanceBreakdownV2Async(
            _context,
            address,
            pricing.Subtotal,
            deliveryQuote.TotalFee,
            discount,
            request.PaymentMethod,
            cancellationToken);

        var allAddresses = await _context.CustomerAddresses
            .AsNoTracking()
            .Where(a => a.UserId == request.UserId)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.UpdatedAtUtc)
            .ThenByDescending(a => a.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return new CheckoutSummaryDto(
            new CheckoutCartDto(
                pricing.Items.Count,
                pricing.Items.Sum(x => x.Quantity),
                pricing.Items,
                pricing.UnavailableItems.Count > 0,
                pricing.UnavailableItems.Count,
                pricing.UnavailableItems.Count > 0,
                pricing.UnavailableItems),
            CheckoutSupport.BuildAddressDto(address),
            CheckoutSupport.BuildAvailableAddressesList(allAddresses),
            CheckoutSupport.BuildDeliverySlots(request.DeliverySlotId),
            CheckoutSupport.BuildPaymentMethods(cardAvailable),
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
            CheckoutSupport.BuildCheckoutLineItems(deliveryQuote, financeBreakdown),
            deliveryQuote.PricingMode,
            financeBreakdown.Totals);
    }

    private async Task<CheckoutSummaryDto> BuildPickupSummaryAsync(
        GetCheckoutSummaryQuery request,
        Cart cart,
        CheckoutSupport.CheckoutPricingSnapshot pricing,
        Guid? pickupBranchId,
        CustomerAddress? address,
        Zadana.Domain.Modules.Marketing.Entities.Coupon? coupon,
        decimal discount,
        bool cardAvailable,
        bool cashOnPickupEnabled,
        CancellationToken cancellationToken)
    {
        var pickupAssessment = await CheckoutSupport.EvaluatePickupAsync(
            _context,
            cart,
            pricing.VendorId,
            pickupBranchId,
            address,
            cancellationToken);
        var pickupBranch = pickupAssessment.BranchId.HasValue
            ? await CheckoutSupport.ValidatePickupBranchAsync(
                _context,
                pricing.VendorId,
                pickupAssessment.BranchId.Value,
                cancellationToken)
            : null;
        var deliveryQuote = CheckoutSupport.BuildNoPricingQuote();
        var financeBreakdown = await CheckoutSupport.ResolveFinanceBreakdownV2Async(
            _context,
            address: null,
            pricing.Subtotal,
            shippingCost: 0m,
            discount,
            request.PaymentMethod,
            cancellationToken,
            FulfillmentType.Pickup);
        var preparationTimeMinutes = await _context.Vendors
            .AsNoTracking()
            .Where(v => v.Id == pricing.VendorId)
            .Select(v => v.PreparationTimeMinutes)
            .FirstOrDefaultAsync(cancellationToken);
        var estimatedPickupWindow = DeliveryEtaPolicy.EstimateCheckoutWindow(
            preparationTimeMinutes,
            0m,
            0m,
            DeliveryEtaOperationalProfile.Default);
        var allAddresses = await _context.CustomerAddresses
            .AsNoTracking()
            .Where(a => a.UserId == request.UserId)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.UpdatedAtUtc)
            .ThenByDescending(a => a.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return new CheckoutSummaryDto(
            new CheckoutCartDto(
                pricing.Items.Count,
                pricing.Items.Sum(x => x.Quantity),
                pricing.Items,
                pricing.UnavailableItems.Count > 0,
                pricing.UnavailableItems.Count,
                pricing.UnavailableItems.Count > 0,
                pricing.UnavailableItems),
            CheckoutSupport.BuildAddressDto(address),
            CheckoutSupport.BuildAvailableAddressesList(allAddresses),
            DeliverySlots: [],
            PaymentMethods: CheckoutSupport.BuildPickupPaymentMethods(cardAvailable, cashOnPickupEnabled),
            CheckoutSupport.BuildPromoCodeDto(coupon, discount),
            pickupAssessment.DeliveryCheck,
            new CheckoutEstimatedDeliveryWindowDto(
                estimatedPickupWindow.MinMinutes,
                estimatedPickupWindow.MaxMinutes,
                estimatedPickupWindow.Confidence,
                estimatedPickupWindow.Source,
                estimatedPickupWindow.IsApproximate,
                estimatedPickupWindow.CalculationMode,
                estimatedPickupWindow.Explanation),
            CheckoutSupport.BuildDeliveryQuoteDto(deliveryQuote),
            CheckoutSupport.BuildDeliveryBreakdownDto(deliveryQuote),
            CheckoutSupport.BuildCheckoutLineItems(deliveryQuote, financeBreakdown),
            deliveryQuote.PricingMode,
            financeBreakdown.Totals,
            FulfillmentType: "pickup",
            PickupBranch: CheckoutSupport.BuildPickupBranchDto(pickupBranch));
    }
}
