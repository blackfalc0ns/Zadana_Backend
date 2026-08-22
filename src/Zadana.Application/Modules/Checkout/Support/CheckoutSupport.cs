using Microsoft.EntityFrameworkCore;
using System.Globalization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Application.Modules.Checkout.DTOs;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Delivery.Support;
using Zadana.Application.Modules.Geography;
using Zadana.Application.Modules.Geography.Support;
using Zadana.Application.Modules.Orders.Support;
using Zadana.Application.Modules.Vendors.Support;
using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Marketing.Entities;
using Zadana.Domain.Modules.Marketing.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Checkout.Support;

internal static class CheckoutSupport
{
    public const string DefaultDeliverySlotId = "standard-30-45";
    public const string Currency = "SAR";

    public static async Task<Cart> GetRequiredCartAsync(
        IApplicationDbContext context,
        Guid userId,
        CancellationToken cancellationToken,
        bool asTracking = false)
    {
        var cart = await CartLookup.FindCartAsync(context, userId, null, cancellationToken, includeItems: true, asTracking: asTracking);
        if (cart == null || cart.Items.Count == 0)
        {
            throw new BusinessRuleException("EMPTY_CART", "Cart is empty.");
        }

        return cart;
    }

    public static async Task<CheckoutPricingSnapshot> BuildPricingSnapshotAsync(
        IApplicationDbContext context,
        Cart cart,
        Guid? selectedVendorId,
        CustomerAddress? address,
        CancellationToken cancellationToken,
        Guid? pickupBranchId = null)
    {
        var masterProductIds = cart.Items.Select(x => x.MasterProductId).Distinct().ToList();
        var requiredQuantityByProductId = cart.Items
            .GroupBy(item => item.MasterProductId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));
        var candidateOffers = await LoadCandidateOffersAsync(
            context,
            masterProductIds,
            selectedVendorId,
            cancellationToken);

        candidateOffers = ApplyUnifiedPricing(candidateOffers);

        IReadOnlyDictionary<Guid, Guid?> selectedBranchIdByVendor;
        if (pickupBranchId.HasValue && selectedVendorId.HasValue)
        {
            selectedBranchIdByVendor = new Dictionary<Guid, Guid?>
            {
                [selectedVendorId.Value] = pickupBranchId.Value
            };
        }
        else
        {
            selectedBranchIdByVendor = await ResolveAddressBranchIdsByVendorAsync(
                context,
                candidateOffers.Select(offer => offer.VendorId),
                address,
                candidateOffers,
                requiredQuantityByProductId,
                cancellationToken);
        }

        candidateOffers = FilterOffersForAddressBranch(candidateOffers, selectedBranchIdByVendor);

        var availabilityVendorIds = selectedVendorId.HasValue
            ? candidateOffers.Select(offer => offer.VendorId).Append(selectedVendorId.Value)
            : candidateOffers.Select(offer => offer.VendorId);
        var availabilityDecisions = await VendorCustomerAvailabilityPolicy.LoadDecisionsAsync(
            context,
            availabilityVendorIds,
            cancellationToken);

        if (selectedVendorId.HasValue)
        {
            var selectedVendorDecision = VendorCustomerAvailabilityPolicy.ResolveOrOffline(availabilityDecisions, selectedVendorId.Value);
            if (!selectedVendorDecision.IsPurchasable && candidateOffers.Count > 0)
            {
                throw new BusinessRuleException(selectedVendorDecision.ReasonCode?.ToUpperInvariant() ?? "VENDOR_OFFLINE", selectedVendorDecision.ReasonMessage ?? "Vendor is temporarily unavailable.");
            }
        }

        var visibleOffers = candidateOffers
            .Where(offer => VendorCustomerAvailabilityPolicy.ResolveOrOffline(availabilityDecisions, offer.VendorId).IsVisibleInCatalog)
            .ToList();
        var purchasableVisibleOffers = visibleOffers
            .Where(offer =>
                requiredQuantityByProductId.TryGetValue(offer.MasterProductId, out var requiredQuantity) &&
                offer.StockQuantity >= requiredQuantity)
            .ToList();

        CandidateVendorSnapshot? candidateVendor;
        if (selectedVendorId.HasValue)
        {
            var offers = purchasableVisibleOffers
                .Where(x => x.VendorId == selectedVendorId.Value)
                .GroupBy(x => x.MasterProductId)
                .Select(offerGroup => offerGroup
                    .OrderBy(x => x.Price)
                    .ThenByDescending(x => x.CreatedAtUtc)
                    .First())
                .ToList();

            candidateVendor = offers.Count > 0
                ? new CandidateVendorSnapshot(
                    selectedVendorId.Value,
                    offers.Count == masterProductIds.Count,
                    offers.Sum(chosen => chosen.Price * cart.Items.First(item => item.MasterProductId == chosen.MasterProductId).Quantity),
                    offers)
                : null;
        }
        else
        {
            candidateVendor = purchasableVisibleOffers
                .GroupBy(x => x.VendorId)
                .Select(group =>
                {
                    var chosenOffers = group
                        .GroupBy(x => x.MasterProductId)
                        .Select(offerGroup => offerGroup
                            .OrderBy(x => x.Price)
                            .ThenByDescending(x => x.CreatedAtUtc)
                            .First())
                        .ToList();

                    var coversAll = chosenOffers.Count == masterProductIds.Count;
                    var total = chosenOffers.Sum(chosen => chosen.Price * cart.Items.First(item => item.MasterProductId == chosen.MasterProductId).Quantity);
                    return new CandidateVendorSnapshot(group.Key, coversAll, total, chosenOffers);
                })
                .Where(x => x.CoversAllProducts)
                .OrderBy(x => x.Total)
                .ThenBy(x => x.VendorId)
                .FirstOrDefault();
        }

        if (candidateVendor == null)
        {
            throw selectedVendorId.HasValue
                ? new BusinessRuleException(
                        "CART_ITEMS_UNAVAILABLE_AT_ADDRESS_BRANCH",
                        BuildUnavailableCartItemsMessage(
                            cart.Items
                            .Where(item => purchasableVisibleOffers.All(offer =>
                                offer.VendorId != selectedVendorId.Value ||
                                offer.MasterProductId != item.MasterProductId))
                            .Select(item => item.ProductName)
                            .Distinct(StringComparer.CurrentCultureIgnoreCase)
                            .ToList()))
                : new BusinessRuleException("CHECKOUT_VENDOR_UNAVAILABLE", "No single vendor can fulfill all cart items for checkout.");
        }

        var unavailableItems = selectedVendorId.HasValue
            ? BuildUnavailableItems(cart, selectedVendorId.Value, visibleOffers, candidateVendor.Offers)
            : new List<CheckoutUnavailableCartItemDto>();

        var items = cart.Items
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.ProductName, StringComparer.CurrentCultureIgnoreCase)
            .Where(item => candidateVendor.Offers.Any(offer => offer.MasterProductId == item.MasterProductId))
            .Select(item =>
            {
                var offer = candidateVendor.Offers.First(x => x.MasterProductId == item.MasterProductId);
                return new CheckoutCartItemDto(
                    item.Id,
                    item.MasterProductId,
                    PickLocalized(offer.CustomNameAr ?? offer.NameAr, offer.CustomNameEn ?? offer.NameEn),
                    offer.ImageUrl,
                    PickLocalizedNullable(offer.UnitAr, offer.UnitEn),
                    item.Quantity,
                    offer.Price,
                    offer.Price * item.Quantity,
                    PickLocalizedNullable(offer.DisplaySizeAr, offer.DisplaySizeEn),
                    PickLocalizedNullable(offer.PackageTypeAr, offer.PackageTypeEn),
                    offer.MeasurementValue,
                    PickLocalizedNullable(offer.MeasurementUnitAr, offer.MeasurementUnitEn),
                    offer.ImageUrl,
                    offer.Images);
            })
            .ToList();

        var branchSelection = await ResolveBranchSelectionAsync(
            context,
            candidateVendor.VendorId,
            candidateVendor.Offers,
            cancellationToken);

        return new CheckoutPricingSnapshot(
            candidateVendor.VendorId,
            branchSelection.BranchId,
            branchSelection.HasAmbiguousBranchScopedOffers,
            branchSelection.RequiresAddressBranchResolution,
            items,
            unavailableItems,
            items.Sum(x => x.TotalPrice));
    }

    public static async Task<Guid?> ResolveDeliveryBranchIdAsync(
        IApplicationDbContext context,
        CheckoutPricingSnapshot pricing,
        CustomerAddress? address,
        CancellationToken cancellationToken)
    {
        if (pricing.VendorBranchId.HasValue)
        {
            return pricing.VendorBranchId.Value;
        }

        if (pricing.HasAmbiguousBranchScopedOffers || !pricing.RequiresAddressBranchResolution)
        {
            return null;
        }

        var branches = await context.VendorBranches
            .AsNoTracking()
            .Where(branch => branch.VendorId == pricing.VendorId && branch.IsActive)
            .OrderByDescending(branch => branch.IsPrimary)
            .ThenBy(branch => branch.CreatedAtUtc)
            .Select(branch => new ActiveBranchSnapshot(
                branch.Id,
                branch.Latitude,
                branch.Longitude,
                branch.DeliveryRadiusKm,
                string.IsNullOrWhiteSpace(branch.City) ? branch.Vendor.City : branch.City,
                branch.IsPrimary,
                branch.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        if (branches.Count == 0)
        {
            return null;
        }

        if (address is null)
        {
            return branches[0].Id;
        }

        var ordered = NearestBranchSelector.Order(
            branches,
            address.Latitude,
            address.Longitude,
            branch => branch.Latitude,
            branch => branch.Longitude,
            branch => branch.IsPrimary,
            branch => branch.CreatedAtUtc).ToList();

        return ordered.Count > 0 ? ordered[0].Id : null;
    }

    public static async Task<CustomerAddress?> ResolveSelectedAddressAsync(
        IApplicationDbContext context,
        Guid userId,
        Guid? addressId,
        CancellationToken cancellationToken)
    {
        if (addressId.HasValue)
        {
            return await context.CustomerAddresses
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == addressId.Value && x.UserId == userId, cancellationToken);
        }

        return await context.CustomerAddresses
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.IsDefault)
            .ThenByDescending(x => x.UpdatedAtUtc)
            .ThenByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public static async Task<Coupon?> ResolveAppliedCouponAsync(
        IApplicationDbContext context,
        Guid userId,
        Cart cart,
        CancellationToken cancellationToken)
    {
        if (!cart.CouponId.HasValue)
        {
            return null;
        }

        var coupon = await context.Coupons
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == cart.CouponId.Value, cancellationToken);

        if (coupon is null)
        {
            return null;
        }

        await EnsureCouponEligibilityAsync(context, coupon, userId, null, cart.Subtotal, cancellationToken);
        return coupon;
    }

    public static async Task<Coupon> ResolveCouponByCodeAsync(
        IApplicationDbContext context,
        Guid userId,
        string code,
        Guid vendorId,
        decimal subtotal,
        CancellationToken cancellationToken)
    {
        var coupon = await context.Coupons
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Code == code.Trim().ToUpperInvariant(), cancellationToken)
            ?? throw new BusinessRuleException("INVALID_PROMO_CODE", "Promo code is invalid.");

        await EnsureCouponEligibilityAsync(context, coupon, userId, vendorId, subtotal, cancellationToken);

        return coupon;
    }

    public static decimal CalculateDiscountAmount(Coupon coupon, decimal subtotal)
    {
        decimal discount = coupon.DiscountType switch
        {
            CouponDiscountType.Percentage => subtotal * coupon.DiscountValue / 100m,
            CouponDiscountType.Fixed => coupon.DiscountValue,
            _ => 0m
        };

        if (coupon.MaxDiscountAmount.HasValue)
        {
            discount = Math.Min(discount, coupon.MaxDiscountAmount.Value);
        }

        return Math.Min(subtotal, decimal.Round(discount, 2, MidpointRounding.AwayFromZero));
    }

    public static decimal ResolveShippingCost(Cart cart) => cart.DeliveryFee;

    public static async Task<DeliveryPriceQuote> QuoteDeliveryOrFallbackAsync(
        IDeliveryPricingService deliveryPricingService,
        Guid? vendorBranchId,
        CustomerAddress? address,
        CancellationToken cancellationToken,
        decimal? orderSubtotal = null)
    {
        if (address is null)
        {
            throw new BusinessRuleException(
                "CUSTOMER_ADDRESS_REQUIRED",
                "A delivery address is required to calculate shipping cost.");
        }

        if (!vendorBranchId.HasValue)
        {
            throw new BusinessRuleException(
                "DELIVERY_PRICING_UNAVAILABLE",
                "Delivery pricing could not be determined because the vendor branch is unknown.");
        }

        return await deliveryPricingService.QuoteAsync(vendorBranchId.Value, address.Id, cancellationToken, orderSubtotal);
    }

    public static async Task<CheckoutDeliveryAssessment> EvaluateDeliveryAsync(
        IApplicationDbContext context,
        IDeliveryPricingService deliveryPricingService,
        Guid? vendorBranchId,
        CustomerAddress? address,
        CancellationToken cancellationToken,
        decimal? orderSubtotal = null)
    {
        if (address is null)
        {
            return new CheckoutDeliveryAssessment(
                new CheckoutDeliveryCheckDto(
                    "address_required",
                    false,
                    false,
                    "اختر عنوانًا لحساب التوصيل.",
                    "Choose an address to calculate delivery.",
                    null,
                    null),
                BuildNoPricingQuote());
        }

        if (!vendorBranchId.HasValue)
        {
            return new CheckoutDeliveryAssessment(
                new CheckoutDeliveryCheckDto(
                    "pricing_unavailable",
                    false,
                    false,
                    "ما قدرنا نحدد التوصيل لهذا العنوان.",
                    "Delivery could not be determined for this address.",
                    null,
                    null),
                BuildNoPricingQuote());
        }

        VendorBranchSnapshot? branch = await context.VendorBranches
            .AsNoTracking()
            .Where(item => item.Id == vendorBranchId.Value)
            .Select(item => new VendorBranchSnapshot(
                item.Id,
                item.Latitude,
                item.Longitude,
                item.DeliveryRadiusKm,
                item.IsActive,
                string.IsNullOrWhiteSpace(item.Region) ? item.Vendor.Region : item.Region,
                string.IsNullOrWhiteSpace(item.City) ? item.Vendor.City : item.City))
            .FirstOrDefaultAsync(cancellationToken);

        if (branch is null || !branch.IsActive)
        {
            return new CheckoutDeliveryAssessment(
                new CheckoutDeliveryCheckDto(
                    "pricing_unavailable",
                    false,
                    false,
                    "ما قدرنا نحدد التوصيل لهذا العنوان.",
                    "Delivery could not be determined for this address.",
                    null,
                    null),
                BuildNoPricingQuote());
        }

        if (await ShouldEnforceOperationalGeographyAsync(context, cancellationToken))
        {
            if (!await OperationalGeographyScope.IsOperationalBranchAsync(context, branch.Region, branch.City, cancellationToken))
            {
                return BuildServiceAreaUnavailableAssessment();
            }

            if (!await OperationalGeographyScope.IsOperationalAddressCityAsync(context, address.City, cancellationToken))
            {
                return BuildServiceAreaUnavailableAssessment();
            }
        }

        try
        {
            var quote = await deliveryPricingService.QuoteAsync(branch.Id, address.Id, cancellationToken, orderSubtotal);
            if (IsOutsideEffectiveDeliveryRadius(branch, quote))
            {
                return new CheckoutDeliveryAssessment(
                    new CheckoutDeliveryCheckDto(
                        "undeliverable",
                        false,
                        false,
                        "هذا المتجر غير متاح للتوصيل إلى العنوان الحالي.",
                        "This store does not deliver to the current address.",
                        null,
                        null),
                    quote);
            }

            if (quote.HasAnomalyWarning)
            {
                return new CheckoutDeliveryAssessment(
                    new CheckoutDeliveryCheckDto(
                        "pricing_anomaly",
                        false,
                        false,
                        "تكلفة التوصيل غير طبيعية لهذا الطلب. جرّب عنوانًا آخر أو تواصل مع الدعم.",
                        "Delivery pricing looks unusual for this order. Try another address or contact support.",
                        null,
                        null),
                    quote);
            }

            return new CheckoutDeliveryAssessment(
                new CheckoutDeliveryCheckDto(
                    "deliverable",
                    true,
                    true,
                    "التوصيل متاح لهذا العنوان.",
                    "Delivery is available for this address.",
                    quote.TotalFee,
                    quote.DistanceKm),
                quote);
        }
        catch (BusinessRuleException exception) when (exception.ErrorCode is "CUSTOMER_ADDRESS_REQUIRED" or "DELIVERY_PRICING_UNAVAILABLE")
        {
            return new CheckoutDeliveryAssessment(
                new CheckoutDeliveryCheckDto(
                    exception.ErrorCode == "CUSTOMER_ADDRESS_REQUIRED" ? "address_required" : "pricing_unavailable",
                    false,
                    false,
                    exception.ErrorCode == "CUSTOMER_ADDRESS_REQUIRED"
                        ? "اختر عنوانًا لحساب التوصيل."
                        : "ما قدرنا نحدد التوصيل لهذا العنوان.",
                    exception.ErrorCode == "CUSTOMER_ADDRESS_REQUIRED"
                        ? "Choose an address to calculate delivery."
                        : "Delivery could not be determined for this address.",
                    null,
                    null),
                BuildNoPricingQuote());
        }
    }

    public static async Task<CheckoutPickupAssessment> EvaluatePickupAsync(
        IApplicationDbContext context,
        Cart cart,
        Guid vendorId,
        Guid? pickupBranchId,
        CustomerAddress? address,
        CancellationToken cancellationToken)
    {
        // Explicit branch selection (classic pickup UX) does not require an address.
        // Nearest-branch auto-select still needs GPS coordinates on the customer address.
        Guid? resolvedBranchId = pickupBranchId;
        if (!resolvedBranchId.HasValue)
        {
            if (address is null)
            {
                return new CheckoutPickupAssessment(
                    null,
                    new CheckoutDeliveryCheckDto(
                        "address_required",
                        false,
                        false,
                        "اختر عنوانًا لتحديد أقرب فرع للاستلام.",
                        "Choose an address to find the nearest pickup branch.",
                        null,
                        null));
            }

            if (!HasUsableCoordinates(address))
            {
                return new CheckoutPickupAssessment(
                    null,
                    new CheckoutDeliveryCheckDto(
                        "address_coordinates_required",
                        false,
                        false,
                        "العنوان يحتاج موقع GPS لتحديد أقرب فرع للاستلام.",
                        "Address needs GPS coordinates to find the nearest pickup branch.",
                        null,
                        null));
            }

            if (await ShouldEnforceOperationalGeographyAsync(context, cancellationToken)
                && !await OperationalGeographyScope.IsOperationalAddressCityAsync(context, address.City, cancellationToken))
            {
                return new CheckoutPickupAssessment(
                    null,
                    new CheckoutDeliveryCheckDto(
                        "service_area_unavailable",
                        false,
                        false,
                        "الاستلام غير متاح في هذه المنطقة حاليًا.",
                        "Pickup is not available in this service area yet.",
                        null,
                        null));
            }

            resolvedBranchId = await ResolvePickupBranchForCartAsync(context, cart, vendorId, address, cancellationToken);
        }

        if (!resolvedBranchId.HasValue)
        {
            return new CheckoutPickupAssessment(
                null,
                new CheckoutDeliveryCheckDto(
                    "pickup_unavailable",
                    false,
                    false,
                    "ما في فرع قريب للاستلام من هذا العنوان.",
                    "No pickup branch is available near this address.",
                    null,
                    null));
        }

        VendorBranch branch;
        try
        {
            branch = await ValidatePickupBranchAsync(context, vendorId, resolvedBranchId.Value, cancellationToken);
        }
        catch (NotFoundException)
        {
            return new CheckoutPickupAssessment(
                null,
                new CheckoutDeliveryCheckDto(
                    "pickup_unavailable",
                    false,
                    false,
                    "ما في فرع قريب للاستلام من هذا العنوان.",
                    "No pickup branch is available near this address.",
                    null,
                    null));
        }

        if (await ShouldEnforceOperationalGeographyAsync(context, cancellationToken)
            && !await OperationalGeographyScope.IsOperationalBranchAsync(
                context,
                branch.Region,
                branch.City,
                cancellationToken))
        {
            return new CheckoutPickupAssessment(
                null,
                new CheckoutDeliveryCheckDto(
                    "service_area_unavailable",
                    false,
                    false,
                    "الاستلام غير متاح من هذا الفرع حاليًا.",
                    "Pickup is not available from this branch yet.",
                    null,
                    null));
        }

        var distanceKm = address is null ? null : CalculateBranchDistanceKm(branch, address);
        return new CheckoutPickupAssessment(
            resolvedBranchId,
            new CheckoutDeliveryCheckDto(
                "pickup_ready",
                true,
                true,
                "الاستلام متاح من أقرب فرع.",
                "Pickup is available from the nearest branch.",
                0m,
                distanceKm));
    }

    public static DeliveryPriceQuote BuildNoPricingQuote() =>
        new(0m, 0m, 0m, 0m, 0m, "zone-fallback", "No pricing", 0m, 0m, 0m, 0m, "fallback", "fallback", true, "fallback", null, "pricing_unavailable", DateTime.UtcNow, 2, false);

    public static CheckoutDeliveryQuoteDto BuildDeliveryQuoteDto(DeliveryPriceQuote quote) =>
        new(
            quote.DistanceKm,
            quote.BaseFee,
            quote.DistanceFee,
            quote.SurgeFee,
            quote.TotalFee,
            quote.PricingMode,
            quote.RuleLabel);

    public static CheckoutDeliveryBreakdownDto BuildDeliveryBreakdownDto(DeliveryPriceQuote quote) =>
        new(
            new CheckoutDeliveryLegDto(quote.DriverToVendorDistanceKm, quote.DriverToVendorFee, quote.DriverToVendorPricingSource),
            new CheckoutDeliveryLegDto(quote.VendorToCustomerDistanceKm, quote.VendorToCustomerFee, quote.VendorToCustomerPricingSource),
            quote.TotalFee,
            quote.PricingMode,
            quote.UsedEstimatedDriverPricing);

    public static async Task<CheckoutFinanceBreakdown> ResolveFinanceBreakdownAsync(
        IApplicationDbContext context,
        CustomerAddress? address,
        decimal subtotal,
        decimal shippingCost,
        decimal discount,
        string? paymentMethodCode,
        CancellationToken cancellationToken,
        FulfillmentType fulfillment = FulfillmentType.Delivery) =>
        await ResolveFinanceBreakdownV2Async(
            context,
            address,
            subtotal,
            shippingCost,
            discount,
            paymentMethodCode,
            cancellationToken,
            fulfillment);

    public static async Task<CheckoutFinanceBreakdown> ResolveFinanceBreakdownV2Async(
        IApplicationDbContext context,
        CustomerAddress? address,
        decimal subtotal,
        decimal shippingCost,
        decimal discount,
        string? paymentMethodCode,
        CancellationToken cancellationToken,
        FulfillmentType fulfillment = FulfillmentType.Delivery)
    {
        var settings = await ResolveZoneOrCityFinanceSettingsAsync(context, address, cancellationToken);
        var normalizedPaymentMethod = NormalizePaymentMethodCode(paymentMethodCode);
        var taxableBase = Math.Max(0m, subtotal + shippingCost - discount);

        var vatAmount = settings.IsVatActive && settings.VatPercent > 0m
            ? decimal.Round(taxableBase * settings.VatPercent / 100m, 2, MidpointRounding.AwayFromZero)
            : 0m;

        // COD fee is delivery-only. Store pickup cash must never include "cash on delivery" fees.
        var codFee = fulfillment != FulfillmentType.Pickup
            && normalizedPaymentMethod == "cash"
            && settings.IsCodFeeActive
            ? CalculateCodFee(settings, taxableBase)
            : 0m;

        return new CheckoutFinanceBreakdown(
            taxableBase,
            vatAmount,
            codFee,
            BuildTotals(subtotal, shippingCost, discount, vatAmount, codFee));
    }

    public static List<CheckoutShippingBreakdownLineDto> BuildShippingBreakdown(
        DeliveryPriceQuote quote,
        CheckoutFinanceBreakdown financeBreakdown)
    {
        var lines = new List<CheckoutShippingBreakdownLineDto>
        {
            new("base_delivery", "رسوم التوصيل الأساسية", "Base delivery", quote.BaseFee),
            new("distance_surcharge", "رسوم المسافة", "Distance surcharge", quote.DistanceFee),
            new("peak_surcharge", "رسوم الذروة", "Peak surcharge", quote.SurgeFee)
        };

        return lines.Where(line => line.Amount > 0m).ToList();
    }

    public static CheckoutPromoCodeDto? BuildPromoCodeDto(Coupon? coupon, decimal discountAmount)
    {
        if (coupon == null)
        {
            return null;
        }

        return new CheckoutPromoCodeDto(
            coupon.Code,
            coupon.DiscountType == CouponDiscountType.Fixed ? "fixed" : "percentage",
            coupon.DiscountValue,
            discountAmount);
    }

    public static List<CheckoutShippingBreakdownLineDto> BuildShippingBreakdownV2(
        DeliveryPriceQuote quote,
        CheckoutFinanceBreakdown financeBreakdown)
    {
        var lines = new List<CheckoutShippingBreakdownLineDto>
        {
            new("driver_to_vendor", "رسوم انتقال المندوب إلى التاجر", "Driver to vendor", quote.DriverToVendorFee),
            new("vendor_to_customer", "رسوم التوصيل من التاجر إلى العميل", "Vendor to customer", quote.VendorToCustomerFee)
        };

        if (quote.SurgeFee > 0m)
        {
            lines.Add(new CheckoutShippingBreakdownLineDto("peak_surcharge", "رسوم الذروة", "Peak surcharge", quote.SurgeFee));
        }

        return lines;
    }

    public static List<CheckoutShippingBreakdownLineDto> BuildFinanceBreakdownLines(
        CheckoutFinanceBreakdown financeBreakdown)
    {
        var lines = new List<CheckoutShippingBreakdownLineDto>();

        if (financeBreakdown.VatAmount > 0m)
        {
            lines.Add(new CheckoutShippingBreakdownLineDto("vat", "ضريبة القيمة المضافة", "VAT", financeBreakdown.VatAmount));
        }

        if (financeBreakdown.CodFee > 0m)
        {
            lines.Add(new CheckoutShippingBreakdownLineDto("cod_fee", "رسوم الدفع عند الاستلام", "Cash on delivery fee", financeBreakdown.CodFee));
        }

        return lines;
    }

    public static List<CheckoutShippingBreakdownLineDto> BuildCheckoutLineItems(
        DeliveryPriceQuote quote,
        CheckoutFinanceBreakdown financeBreakdown)
    {
        var lines = BuildShippingBreakdownV2(quote, financeBreakdown);
        lines.AddRange(BuildFinanceBreakdownLines(financeBreakdown));
        return lines;
    }

    public static CheckoutTotalsDto BuildTotals(decimal subtotal, decimal shippingCost, decimal discount) =>
        BuildTotals(subtotal, shippingCost, discount, 0m, 0m);

    public static CheckoutTotalsDto BuildTotals(
        decimal subtotal,
        decimal shippingCost,
        decimal discount,
        decimal vatAmount,
        decimal codFee) =>
        new(
            subtotal,
            shippingCost,
            discount,
            vatAmount,
            codFee,
            Math.Max(0m, subtotal + shippingCost - discount + vatAmount + codFee),
            Currency);

    public static List<CheckoutDeliverySlotDto> BuildDeliverySlots(string? selectedSlotId)
    {
        var selected = string.IsNullOrWhiteSpace(selectedSlotId) ? DefaultDeliverySlotId : selectedSlotId.Trim();
        return
        [
            new CheckoutDeliverySlotDto(
                DefaultDeliverySlotId,
                "٣٠-٤٥ دقيقة",
                "30-45 minutes",
                DateTime.UtcNow.AddMinutes(30),
                DateTime.UtcNow.AddMinutes(45),
                true,
                string.Equals(selected, DefaultDeliverySlotId, StringComparison.OrdinalIgnoreCase))
        ];
    }

    public static List<CheckoutPaymentMethodDto> BuildPaymentMethods(bool cardAvailable) =>
    [
        new CheckoutPaymentMethodDto("card", "بطاقة ائتمان / مدى", "Credit / Debit Card", "فيزا، ماستركارد، مدى", "Visa, Mastercard, Mada", cardAvailable, cardAvailable),
        new CheckoutPaymentMethodDto("apple_pay", "Apple Pay", "Apple Pay", "دفع سريع وآمن", "Fast and secure payment", cardAvailable, false),
        new CheckoutPaymentMethodDto("cash", "الدفع عند الاستلام", "Cash on Delivery", "ادفع كاش وقت استلام الطلب", "Pay cash when you receive the order", true, !cardAvailable),
        new CheckoutPaymentMethodDto("bank", "تحويل بنكي", "Bank Transfer", "تحويل مباشر من البنك", "Direct transfer from bank", true, false)
    ];

    public static CheckoutSelectedAddressDto? BuildAddressDto(CustomerAddress? address)
    {
        if (address == null)
        {
            return null;
        }

        return new CheckoutSelectedAddressDto(
            address.Id,
            address.Label?.ToString().ToLowerInvariant() ?? AddressLabel.Other.ToString().ToLowerInvariant(),
            address.AddressLine,
            address.IsDefault);
    }

    public static List<CheckoutSelectedAddressDto> BuildAvailableAddressesList(
        IEnumerable<CustomerAddress> addresses) =>
        addresses.Select(address => new CheckoutSelectedAddressDto(
            address.Id,
            address.Label?.ToString().ToLowerInvariant() ?? AddressLabel.Other.ToString().ToLowerInvariant(),
            address.AddressLine,
            address.IsDefault)).ToList();

    public static string? NormalizePaymentMethodCode(string? paymentMethodCode) =>
        paymentMethodCode?.Trim().ToLowerInvariant() switch
        {
            null or "" => null,
            "card" or "credit_card" or "creditcard" or "debit_card" or "debitcard" or "mada" => "card",
            "cash" or "cash_on_delivery" or "cashondelivery" or "cod" => "cash",
            "bank" or "bank_transfer" or "banktransfer" => "bank",
            "apple_pay" or "applepay" => "apple_pay",
            "wallet" => throw new BusinessRuleException("PAYMENT_METHOD_NOT_SUPPORTED", "Wallet payment is not supported yet."),
            _ => throw new BusinessRuleException("PAYMENT_METHOD_NOT_SUPPORTED", "Selected payment method is not supported.")
        };

    public static string MapPaymentMethodCodeToEnumName(string paymentMethodCode) =>
        NormalizePaymentMethodCode(paymentMethodCode) switch
        {
            "card" => "Card",
            "cash" => "CashOnDelivery",
            "bank" => "BankTransfer",
            "apple_pay" => "ApplePay",
            _ => throw new BusinessRuleException("PAYMENT_METHOD_NOT_SUPPORTED", "Selected payment method is not supported.")
        };

    public static string MapOrderStatusToContractValue(string orderStatus) =>
        orderStatus switch
        {
            "PendingVendorAcceptance" => "processing",
            "Accepted" => "processing",
            "Preparing" => "processing",
            "ReadyForPickup" => "processing",
            "DriverAssignmentInProgress" => "processing",
            "DriverAssigned" => "processing",
            "PickedUp" => "processing",
            "OnTheWay" => "processing",
            _ => "pending"
        };

    public static string MapPaymentStatusToContractValue(string paymentStatus) =>
        paymentStatus switch
        {
            "Paid" => "paid",
            "Failed" => "failed",
            _ => "pending"
        };

    private static async Task<BranchSelection> ResolveBranchSelectionAsync(
        IApplicationDbContext context,
        Guid vendorId,
        IReadOnlyCollection<VendorOfferSnapshot> offers,
        CancellationToken cancellationToken)
    {
        var nonNullBranchIds = offers
            .Where(x => x.VendorBranchId.HasValue)
            .Select(x => x.VendorBranchId!.Value)
            .Distinct()
            .ToList();

        if (nonNullBranchIds.Count == 1)
        {
            return new BranchSelection(nonNullBranchIds[0], false, false);
        }

        if (nonNullBranchIds.Count > 1)
        {
            return new BranchSelection(null, true, false);
        }

        var activeBranchIds = await context.VendorBranches
            .AsNoTracking()
            .Where(branch => branch.VendorId == vendorId && branch.IsActive)
            .OrderBy(branch => branch.CreatedAtUtc)
            .Select(branch => branch.Id)
            .ToListAsync(cancellationToken);

        return activeBranchIds.Count switch
        {
            1 => new BranchSelection(activeBranchIds[0], false, false),
            > 1 => new BranchSelection(null, false, true),
            _ => new BranchSelection(null, false, false)
        };
    }

    private static List<VendorOfferSnapshot> ApplyUnifiedPricing(List<VendorOfferSnapshot> offers)
    {
        if (offers.Count == 0)
        {
            return offers;
        }

        var canonicalPricingByProduct = offers
            .GroupBy(offer => new { offer.VendorId, offer.MasterProductId })
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(offer => offer.IsPrimaryBranch)
                    .ThenBy(offer => offer.VendorBranchId.HasValue)
                    .ThenBy(offer => offer.CreatedAtUtc)
                    .First().Price);

        return offers
            .Select(offer =>
            {
                var price = canonicalPricingByProduct[new { offer.VendorId, offer.MasterProductId }];
                return offer.Price == price ? offer : offer with { Price = price };
            })
            .ToList();
    }

    private static async Task<IReadOnlyDictionary<Guid, Guid?>> ResolveAddressBranchIdsByVendorAsync(
        IApplicationDbContext context,
        IEnumerable<Guid> vendorIds,
        CustomerAddress? address,
        IReadOnlyList<VendorOfferSnapshot> offers,
        IReadOnlyDictionary<Guid, int> requiredQuantityByProductId,
        CancellationToken cancellationToken)
    {
        if (address is null)
        {
            return new Dictionary<Guid, Guid?>();
        }

        var distinctVendorIds = vendorIds.Distinct().ToArray();
        if (distinctVendorIds.Length == 0)
        {
            return new Dictionary<Guid, Guid?>();
        }

        var branches = await context.VendorBranches
            .AsNoTracking()
            .Where(branch => distinctVendorIds.Contains(branch.VendorId) && branch.IsActive)
            .OrderByDescending(branch => branch.IsPrimary)
            .ThenBy(branch => branch.CreatedAtUtc)
            .Select(branch => new AddressBranchCandidate(
                branch.VendorId,
                branch.Id,
                branch.Latitude,
                branch.Longitude,
                branch.DeliveryRadiusKm,
                string.IsNullOrWhiteSpace(branch.City) ? branch.Vendor.City : branch.City,
                branch.IsPrimary,
                branch.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        var offersByVendor = offers
            .GroupBy(offer => offer.VendorId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<VendorOfferSnapshot>)group.ToList());

        return branches
            .GroupBy(branch => branch.VendorId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var vendorBranches = group.ToList();
                    if (!offersByVendor.TryGetValue(group.Key, out var vendorOffers))
                    {
                        vendorOffers = [];
                    }

                    var fulfillableBranch = OrderBranchesForAddress(vendorBranches, address)
                        .FirstOrDefault(branch =>
                            EvaluateBranchCartAvailability(
                                vendorOffers,
                                group.Key,
                                branch.Id,
                                requiredQuantityByProductId).CanFulfillCart);

                    return (Guid?)(fulfillableBranch ?? ResolveBestBranchForAddress(vendorBranches, address))?.Id;
                });
    }

    private static List<VendorOfferSnapshot> FilterOffersForAddressBranch(
        List<VendorOfferSnapshot> offers,
        IReadOnlyDictionary<Guid, Guid?> selectedBranchIdByVendor)
    {
        if (offers.Count == 0 || selectedBranchIdByVendor.Count == 0)
        {
            return offers;
        }

        return offers
            .GroupBy(offer => new { offer.VendorId, offer.MasterProductId })
            .SelectMany(group =>
            {
                if (!selectedBranchIdByVendor.TryGetValue(group.Key.VendorId, out var selectedBranchId) ||
                    !selectedBranchId.HasValue)
                {
                    return group;
                }

                var branchOffers = group
                    .Where(offer => offer.VendorBranchId == selectedBranchId.Value)
                    .ToList();

                if (branchOffers.Count > 0)
                {
                    return branchOffers;
                }

                var hasBranchScopedInventory = group.Any(offer => offer.VendorBranchId.HasValue);
                return hasBranchScopedInventory
                    ? []
                    : group.Where(offer => !offer.VendorBranchId.HasValue);
            })
            .ToList();
    }

    private static AddressBranchCandidate? ResolveBestBranchForAddress(
        IReadOnlyCollection<AddressBranchCandidate> branches,
        CustomerAddress address)
    {
        return OrderBranchesForAddress(branches, address).FirstOrDefault();
    }

    private static IEnumerable<AddressBranchCandidate> OrderBranchesForAddress(
        IReadOnlyCollection<AddressBranchCandidate> branches,
        CustomerAddress address) =>
        NearestBranchSelector.Order(
            branches,
            address.Latitude,
            address.Longitude,
            branch => branch.Latitude,
            branch => branch.Longitude,
            branch => branch.DeliveryRadiusKm,
            branch => branch.IsPrimary,
            branch => branch.CreatedAtUtc);

    private static bool HasUsableCoordinates(CustomerAddress address) =>
        address.Latitude.HasValue &&
        address.Longitude.HasValue &&
        HasUsableCoordinates(address.Latitude.Value, address.Longitude.Value);

    private static bool HasUsableCoordinates(decimal latitude, decimal longitude) =>
        !(latitude == 0m && longitude == 0m);

    private static bool IsArabic() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase);

    private static List<CheckoutUnavailableCartItemDto> BuildUnavailableItems(
        Cart cart,
        Guid selectedVendorId,
        IReadOnlyCollection<VendorOfferSnapshot> visibleOffers,
        IReadOnlyCollection<VendorOfferSnapshot> selectedOffers)
    {
        var selectedProductIds = selectedOffers
            .Select(offer => offer.MasterProductId)
            .ToHashSet();

        return cart.Items
            .Where(item => !selectedProductIds.Contains(item.MasterProductId))
            .OrderBy(item => item.CreatedAtUtc)
            .ThenBy(item => item.ProductName, StringComparer.CurrentCultureIgnoreCase)
            .Select(item =>
            {
                var hasInsufficientStock = visibleOffers.Any(offer =>
                    offer.VendorId == selectedVendorId &&
                    offer.MasterProductId == item.MasterProductId &&
                    offer.StockQuantity < item.Quantity);

                return new CheckoutUnavailableCartItemDto(
                    item.Id,
                    item.MasterProductId,
                    item.ProductName,
                    item.Quantity,
                    hasInsufficientStock ? "insufficient_stock" : "unavailable_at_selected_vendor");
            })
            .ToList();
    }

    private static string BuildUnavailableCartItemsMessage(IReadOnlyCollection<string> productNames)
    {
        var names = string.Join(", ", productNames.Where(name => !string.IsNullOrWhiteSpace(name)).Take(5));
        if (IsArabic())
        {
            return string.IsNullOrWhiteSpace(names)
                ? "بعض المنتجات في العربة غير متوفرة في فرع المتجر المناسب لعنوانك."
                : $"المنتجات التالية غير متوفرة في فرع المتجر المناسب لعنوانك: {names}";
        }
        if (string.IsNullOrWhiteSpace(names))
        {
            return IsArabic()
                ? "بعض المنتجات في العربة غير متوفرة في فرع المتجر المطابق لعنوانك."
                : "Some cart items are unavailable at the store branch matching your address.";
        }

        return IsArabic()
            ? $"المنتجات التالية غير متوفرة في فرع المتجر المطابق لعنوانك: {names}"
            : $"The following products are unavailable at the store branch matching your address: {names}";
    }

    private static string PickLocalized(string? arabic, string? english)
    {
        var preferred = IsArabic() ? arabic : english;
        var fallback = IsArabic() ? english : arabic;
        return preferred?.Trim() ?? fallback?.Trim() ?? string.Empty;
    }

    private static string? PickLocalizedNullable(string? arabic, string? english)
    {
        var value = PickLocalized(arabic, english);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static decimal CalculateCodFee(ZoneFinanceSettingsSnapshot settings, decimal taxableBase)
    {
        var codFee = string.Equals(settings.CodFeeType, "percent", StringComparison.OrdinalIgnoreCase)
            ? taxableBase * settings.CodPercent / 100m
            : settings.CodFlatFee;

        return Math.Max(0m, decimal.Round(codFee, 2, MidpointRounding.AwayFromZero));
    }

    private static async Task<ZoneFinanceSettingsSnapshot> ResolveZoneFinanceSettingsAsync(
        IApplicationDbContext context,
        CustomerAddress? address,
        CancellationToken cancellationToken)
    {
        var zones = await context.DeliveryZones
            .AsNoTracking()
            .Where(zone => zone.IsActive)
            .ToListAsync(cancellationToken);

        DeliveryZone? selectedZone = null;

        if (address?.Latitude.HasValue == true && address.Longitude.HasValue && zones.Count > 0)
        {
            selectedZone = zones
                .Where(zone => IsPointWithinZone(zone, address.Latitude.Value, address.Longitude.Value))
                .OrderBy(zone => ApproximateDistanceKm(zone.CenterLat, zone.CenterLng, address.Latitude.Value, address.Longitude.Value))
                .FirstOrDefault();

            selectedZone ??= zones
                .Where(zone => string.IsNullOrWhiteSpace(address.City) || string.Equals(zone.City, address.City, StringComparison.OrdinalIgnoreCase))
                .OrderBy(zone => ApproximateDistanceKm(zone.CenterLat, zone.CenterLng, address.Latitude.Value, address.Longitude.Value))
                .FirstOrDefault();
        }

        selectedZone ??= zones.FirstOrDefault(zone =>
            !string.IsNullOrWhiteSpace(address?.City) &&
            string.Equals(zone.City, address.City, StringComparison.OrdinalIgnoreCase));

        if (selectedZone is null)
        {
            return ZoneFinanceSettingsSnapshot.Default;
        }

        var settings = await context.ZoneFinanceSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.DeliveryZoneId == selectedZone.Id, cancellationToken);

        return settings is null
            ? ZoneFinanceSettingsSnapshot.Default
            : new ZoneFinanceSettingsSnapshot(
                settings.VatPercent,
                settings.CodFeeType,
                settings.CodFlatFee,
                settings.CodPercent,
                settings.IsVatActive,
                settings.IsCodFeeActive);
    }

    private static async Task<ZoneFinanceSettingsSnapshot> ResolveZoneOrCityFinanceSettingsAsync(
        IApplicationDbContext context,
        CustomerAddress? address,
        CancellationToken cancellationToken)
    {
        var zoneSettings = await ResolveZoneFinanceSettingsAsync(context, address, cancellationToken);
        if (zoneSettings != ZoneFinanceSettingsSnapshot.Default)
        {
            return zoneSettings;
        }

        if (string.IsNullOrWhiteSpace(address?.City))
        {
            return ZoneFinanceSettingsSnapshot.Default;
        }

        var normalizedCity = NormalizeCityName(address.City);
        var cities = await context.SaudiCities
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var city = cities.FirstOrDefault(item =>
            string.Equals(item.Code, address.City, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(NormalizeCityName(item.NameAr), normalizedCity, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(NormalizeCityName(item.NameEn), normalizedCity, StringComparison.OrdinalIgnoreCase));

        if (city is null)
        {
            return ZoneFinanceSettingsSnapshot.Default;
        }

        var citySettings = await context.CityDeliveryPricingSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.SaudiCityId == city.Id, cancellationToken);

        if (citySettings is not null)
        {
            return new ZoneFinanceSettingsSnapshot(
                citySettings.VatPercent,
                citySettings.CodFeeType,
                citySettings.CodFlatFee,
                citySettings.CodPercent,
                citySettings.IsVatActive,
                citySettings.IsCodFeeActive);
        }

        var regionSettings = await context.RegionDeliveryPricingSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.SaudiRegionId == city.RegionId, cancellationToken);

        if (regionSettings is not null)
        {
            return new ZoneFinanceSettingsSnapshot(
                regionSettings.VatPercent,
                regionSettings.CodFeeType,
                regionSettings.CodFlatFee,
                regionSettings.CodPercent,
                regionSettings.IsVatActive,
                regionSettings.IsCodFeeActive);
        }

        var defaults = await context.DeliveryPricingDefaults
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        return defaults is null
            ? ZoneFinanceSettingsSnapshot.Default
            : new ZoneFinanceSettingsSnapshot(
                defaults.VatPercent,
                defaults.CodFeeType,
                defaults.CodFlatFee,
                defaults.CodPercent,
                defaults.IsVatActive,
                defaults.IsCodFeeActive);
    }

    private static string? NormalizeCityName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant()
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty)
            .Replace("_", string.Empty)
            .Replace("أ", "ا")
            .Replace("إ", "ا")
            .Replace("آ", "ا")
            .Replace("ى", "ي")
            .Replace("ة", "ه");
    }

    private static bool IsPointWithinZone(DeliveryZone zone, decimal latitude, decimal longitude) =>
        ApproximateDistanceKm(zone.CenterLat, zone.CenterLng, latitude, longitude) <= zone.RadiusKm;

    private static decimal ApproximateDistanceKm(decimal lat1, decimal lng1, decimal lat2, decimal lng2)
    {
        var dLat = (double)(lat2 - lat1) * Math.PI / 180;
        var dLng = (double)(lng2 - lng1) * Math.PI / 180;
        var avgLat = (double)(lat1 + lat2) / 2 * Math.PI / 180;

        var x = dLng * Math.Cos(avgLat);
        var y = dLat;
        var distanceKm = Math.Sqrt(x * x + y * y) * 6371;

        return (decimal)distanceKm;
    }

    private static bool IsOutsideEffectiveDeliveryRadius(VendorBranchSnapshot branch, DeliveryPriceQuote quote)
    {
        var effectiveRadiusKm = DeliveryProximityLimits.ResolveEffectiveDeliveryRadiusKm(branch.DeliveryRadiusKm);
        return quote.VendorToCustomerDistanceKm > effectiveRadiusKm;
    }

    private static async Task<bool> ShouldEnforceOperationalGeographyAsync(
        IApplicationDbContext context,
        CancellationToken cancellationToken) =>
        await context.SaudiRegions
            .AsNoTracking()
            .AnyAsync(cancellationToken);

    private static CheckoutDeliveryAssessment BuildServiceAreaUnavailableAssessment() =>
        new(
            new CheckoutDeliveryCheckDto(
                "service_area_unavailable",
                false,
                false,
                "التوصيل غير متاح في هذه المنطقة حاليًا.",
                "Delivery is not available in this service area yet.",
                null,
                null),
            BuildNoPricingQuote());

    private static decimal? CalculateBranchDistanceKm(VendorBranch branch, CustomerAddress address)
    {
        if (!HasUsableCoordinates(address) ||
            !HasUsableCoordinates(branch.Latitude, branch.Longitude))
        {
            return null;
        }

        return GeoDistance.Kilometers(
            branch.Latitude,
            branch.Longitude,
            address.Latitude!.Value,
            address.Longitude!.Value);
    }

    private static bool IsSameCityDelivery(string? branchCity, string? customerCity)
    {
        var normalizedBranchCity = NormalizeCityKey(branchCity);
        var normalizedCustomerCity = NormalizeCityKey(customerCity);

        return !string.IsNullOrWhiteSpace(normalizedBranchCity) &&
               !string.IsNullOrWhiteSpace(normalizedCustomerCity) &&
               string.Equals(normalizedBranchCity, normalizedCustomerCity, StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeCityKey(string? value)
    {
        var normalized = NormalizeCityName(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized switch
        {
            "الدمام" or "دمام" or "dammam" => "dammam",
            "الرياض" or "رياض" or "riyadh" => "riyadh",
            "جده" or "جدة" or "جدا" or "jeddah" => "jeddah",
            "مكه" or "مكة" or "mecca" or "makkah" => "makkah",
            "المدينه" or "المدينة" or "مدينه" or "مدينة" or "madinah" or "medina" => "madinah",
            "الخبر" or "خبر" or "khobar" or "alkhobar" => "khobar",
            "الظهران" or "ظهران" or "dhahran" => "dhahran",
            "الجبيل" or "جبيل" or "jubail" or "jubel" => "jubail",
            "القطيف" or "قطيف" or "qatif" or "alqatif" => "qatif",
            "الاحساء" or "الأحساء" or "احساء" or "أحساء" or "الهفوف" or "هفوف" or "ahsa" or "alahsa" or "alhasa" or "hofuf" or "alhofuf" => "ahsa",
            "حفرالباطن" or "حفر" or "hafr" or "hafralbatn" or "hafr_al_batin" or "hafralbatin" => "hafralbatin",
            "رأستنورة" or "راستنورة" or "رأستنوره" or "rastanura" or "rastanorah" => "rastanura",
            "الخفجي" or "خفجي" or "khafji" or "alkhafji" => "khafji",
            "بقيق" or "buqayq" or "abqaiq" => "abqaiq",
            "النعيرية" or "نعيرية" or "nairyah" or "nuayriyah" => "nairyah",
            "سيهات" or "saihat" or "sayhat" => "saihat",
            "تاروت" or "tarut" or "tarout" => "tarut",
            "صفوى" or "صفوا" or "safwa" => "safwa",
            "العوامية" or "عوامية" or "awamiyah" => "awamiyah",
            "رحيمة" or "rahima" or "rahimah" => "rahima",
            "الطائف" or "طائف" or "taif" => "taif",
            "تبوك" or "tabuk" => "tabuk",
            "ابها" or "أبها" or "abha" => "abha",
            "حائل" or "حايل" or "hail" or "ha'il" => "hail",
            "جازان" or "جيزان" or "jazan" or "jizan" => "jazan",
            "نجران" or "najran" => "najran",
            "بريده" or "بريدة" or "buraidah" or "buraydah" => "buraidah",
            "ينبع" or "yanbu" or "yanbuu" => "yanbu",
            _ => normalized
        };
    }

    internal sealed record CheckoutPricingSnapshot(
        Guid VendorId,
        Guid? VendorBranchId,
        bool HasAmbiguousBranchScopedOffers,
        bool RequiresAddressBranchResolution,
        List<CheckoutCartItemDto> Items,
        List<CheckoutUnavailableCartItemDto> UnavailableItems,
        decimal Subtotal);

    internal sealed record CheckoutFinanceBreakdown(
        decimal TaxableBase,
        decimal VatAmount,
        decimal CodFee,
        CheckoutTotalsDto Totals);

    internal sealed record CheckoutDeliveryAssessment(
        CheckoutDeliveryCheckDto DeliveryCheck,
        DeliveryPriceQuote DeliveryQuote);

    internal sealed record CheckoutPickupAssessment(
        Guid? BranchId,
        CheckoutDeliveryCheckDto DeliveryCheck);

    private sealed record ZoneFinanceSettingsSnapshot(
        decimal VatPercent,
        string CodFeeType,
        decimal CodFlatFee,
        decimal CodPercent,
        bool IsVatActive,
        bool IsCodFeeActive)
    {
        public static ZoneFinanceSettingsSnapshot Default { get; } = new(15m, "flat", 10m, 0m, true, true);
    }

    private sealed record VendorOfferSnapshot(
        Guid Id,
        Guid VendorId,
        Guid? VendorBranchId,
        bool IsPrimaryBranch,
        Guid MasterProductId,
        int StockQuantity,
        decimal Price,
        DateTime CreatedAtUtc,
        string? CustomNameAr,
        string? CustomNameEn,
        string NameAr,
        string NameEn,
        List<string> Images,
        string? DisplaySizeAr,
        string? DisplaySizeEn,
        string? PackageTypeAr,
        string? PackageTypeEn,
        decimal? MeasurementValue,
        string? MeasurementUnitAr,
        string? MeasurementUnitEn,
        string? UnitAr,
        string? UnitEn)
    {
        public string? ImageUrl => Images.FirstOrDefault();
    }

    private sealed record CandidateVendorSnapshot(
        Guid VendorId,
        bool CoversAllProducts,
        decimal Total,
        List<VendorOfferSnapshot> Offers);

    private sealed record BranchSelection(
        Guid? BranchId,
        bool HasAmbiguousBranchScopedOffers,
        bool RequiresAddressBranchResolution);

    private sealed record ActiveBranchSnapshot(
        Guid Id,
        decimal Latitude,
        decimal Longitude,
        decimal DeliveryRadiusKm,
        string? City,
        bool IsPrimary,
        DateTime CreatedAtUtc);

    private sealed record BranchDistanceSnapshot(
        ActiveBranchSnapshot Branch,
        decimal DistanceKm,
        bool IsInsideRadius);

    private sealed record AddressBranchCandidate(
        Guid VendorId,
        Guid Id,
        decimal Latitude,
        decimal Longitude,
        decimal DeliveryRadiusKm,
        string? City,
        bool IsPrimary,
        DateTime CreatedAtUtc);

    private sealed record AddressBranchDistance(
        AddressBranchCandidate Branch,
        decimal DistanceKm,
        bool IsInsideRadius);

    private sealed record VendorBranchSnapshot(
        Guid Id,
        decimal Latitude,
        decimal Longitude,
        decimal DeliveryRadiusKm,
        bool IsActive,
        string? Region,
        string? City);

    private static async Task EnsureCouponEligibilityAsync(
        IApplicationDbContext context,
        Coupon coupon,
        Guid userId,
        Guid? vendorId,
        decimal subtotal,
        CancellationToken cancellationToken)
    {
        if (!coupon.IsValid())
        {
            throw new BusinessRuleException("INVALID_PROMO_CODE", "Promo code is invalid or inactive.");
        }

        if (!coupon.IsAssignedTo(userId))
        {
            throw new BusinessRuleException("PROMO_CODE_NOT_APPLICABLE", "Promo code is not assigned to the current customer.");
        }

        if (coupon.MinOrderAmount.HasValue && subtotal < coupon.MinOrderAmount.Value)
        {
            throw new BusinessRuleException("PROMO_MIN_ORDER_NOT_MET", "Promo code minimum order amount is not met.");
        }

        if (coupon.UsageLimit.HasValue)
        {
            var usageCount = await context.Orders
                .AsNoTracking()
                .CountAsync(order => order.CouponId == coupon.Id, cancellationToken);

            if (usageCount >= coupon.UsageLimit.Value)
            {
                throw new BusinessRuleException("PROMO_CODE_USAGE_LIMIT_REACHED", "Promo code usage limit has been reached.");
            }
        }

        if (coupon.PerUserLimit.HasValue)
        {
            var perUserUsageCount = await context.Orders
                .AsNoTracking()
                .CountAsync(order => order.CouponId == coupon.Id && order.UserId == userId, cancellationToken);

            if (perUserUsageCount >= coupon.PerUserLimit.Value)
            {
                throw new BusinessRuleException("PROMO_CODE_USER_LIMIT_REACHED", "Promo code has already been used by this customer.");
            }
        }

        var hasRestrictions = await context.CouponVendors
            .AsNoTracking()
            .AnyAsync(x => x.CouponId == coupon.Id, cancellationToken);

        if (hasRestrictions)
        {
            if (!vendorId.HasValue)
            {
                throw new BusinessRuleException("PROMO_CODE_NOT_APPLICABLE", "Promo code is not applicable without a selected vendor.");
            }

            var isApplicable = await context.CouponVendors
                .AsNoTracking()
                .AnyAsync(x => x.CouponId == coupon.Id && x.VendorId == vendorId.Value, cancellationToken);
            if (!isApplicable)
            {
                throw new BusinessRuleException("PROMO_CODE_NOT_APPLICABLE", "Promo code is not applicable to the selected vendor.");
            }
        }
    }

    public static FulfillmentType ResolveFulfillmentType(string? fulfillmentType) =>
        Enum.TryParse<FulfillmentType>(fulfillmentType, true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : FulfillmentType.Delivery;

    public static async Task<PlatformPickupSettings> LoadPlatformPickupSettingsAsync(
        IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var settings = await context.PlatformPickupSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == PlatformPickupSettings.SingletonId, cancellationToken);

        return settings ?? new PlatformPickupSettings();
    }

    public static async Task<VendorBranch> ValidatePickupBranchAsync(
        IApplicationDbContext context,
        Guid vendorId,
        Guid vendorBranchId,
        CancellationToken cancellationToken)
    {
        var branch = await context.VendorBranches
            .AsNoTracking()
            .Include(item => item.Vendor)
            .Include(item => item.OperatingHours)
            .FirstOrDefaultAsync(item => item.Id == vendorBranchId, cancellationToken)
            ?? throw new NotFoundException("VendorBranch", vendorBranchId);

        if (branch.VendorId != vendorId)
        {
            throw new BusinessRuleException(
                "PICKUP_BRANCH_VENDOR_MISMATCH",
                "Selected pickup branch does not belong to the checkout vendor.");
        }

        if (!branch.IsActive)
        {
            throw new BusinessRuleException(
                "PICKUP_BRANCH_INACTIVE",
                "Selected pickup branch is not active.");
        }

        return branch;
    }

    public static async Task<CheckoutPickupBranchesDto> GetPickupBranchesForCityAsync(
        IApplicationDbContext context,
        Guid userId,
        Guid vendorId,
        string city,
        CancellationToken cancellationToken,
        decimal? customerLatitude = null,
        decimal? customerLongitude = null)
    {
        if (string.IsNullOrWhiteSpace(city))
        {
            throw new BusinessRuleException("PICKUP_CITY_REQUIRED", "City is required to list pickup branches.");
        }

        var vendorExists = await context.Vendors
            .AsNoTracking()
            .AnyAsync(item => item.Id == vendorId && item.Status == VendorStatus.Active, cancellationToken);
        if (!vendorExists)
        {
            throw new NotFoundException("Vendor", vendorId);
        }

        var cart = await GetRequiredCartAsync(context, userId, cancellationToken);
        var masterProductIds = cart.Items.Select(item => item.MasterProductId).Distinct().ToList();
        var requiredQuantityByProductId = cart.Items
            .GroupBy(item => item.MasterProductId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));

        var offers = ApplyUnifiedPricing(
            await LoadCandidateOffersAsync(context, masterProductIds, vendorId, cancellationToken));

        var branches = await context.VendorBranches
            .AsNoTracking()
            .Include(item => item.Vendor)
            .Include(item => item.OperatingHours)
            .Where(item => item.VendorId == vendorId && item.IsActive)
            .OrderByDescending(item => item.IsPrimary)
            .ThenBy(item => item.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var cityBranches = GeoDistance.HasUsableCoordinates(customerLatitude, customerLongitude)
            ? NearestBranchSelector.Order(
                branches,
                customerLatitude,
                customerLongitude,
                branch => branch.Latitude,
                branch => branch.Longitude,
                branch => branch.IsPrimary,
                branch => branch.CreatedAtUtc).ToList()
            : [];

        var options = cityBranches
            .Select(branch =>
            {
                var availability = EvaluateBranchCartAvailability(
                    offers,
                    vendorId,
                    branch.Id,
                    requiredQuantityByProductId);
                var dto = BuildPickupBranchDto(branch)!;
                return new CheckoutPickupBranchOptionDto(
                    dto.Id,
                    dto.Name,
                    dto.AddressLine,
                    dto.City,
                    dto.Address,
                    dto.HoursToday,
                    branch.IsPrimary,
                    availability.CanFulfillCart,
                    availability.MissingItemsCount);
            })
            .OrderByDescending(item => item.CanFulfillCart)
            .ThenByDescending(item => item.IsPrimary)
            .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return new CheckoutPickupBranchesDto(vendorId, city.Trim(), options);
    }

    public static async Task<Guid?> ResolvePickupBranchForCartAsync(
        IApplicationDbContext context,
        Cart cart,
        Guid vendorId,
        CustomerAddress? address,
        CancellationToken cancellationToken)
    {
        var masterProductIds = cart.Items.Select(item => item.MasterProductId).Distinct().ToList();
        var requiredQuantityByProductId = cart.Items
            .GroupBy(item => item.MasterProductId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));

        var offers = ApplyUnifiedPricing(
            await LoadCandidateOffersAsync(context, masterProductIds, vendorId, cancellationToken));

        var branches = await context.VendorBranches
            .AsNoTracking()
            .Where(item => item.VendorId == vendorId && item.IsActive)
            .OrderByDescending(item => item.IsPrimary)
            .ThenBy(item => item.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        if (branches.Count == 0)
        {
            return null;
        }

        var orderedCandidates = NearestBranchSelector.Order(
            branches,
            address?.Latitude,
            address?.Longitude,
            branch => branch.Latitude,
            branch => branch.Longitude,
            branch => branch.DeliveryRadiusKm,
            branch => branch.IsPrimary,
            branch => branch.CreatedAtUtc).ToList();

        if (orderedCandidates.Count == 0)
        {
            return null;
        }

        var fulfillableBranch = orderedCandidates
            .Select(branch => new
            {
                Branch = branch,
                Availability = EvaluateBranchCartAvailability(
                    offers,
                    vendorId,
                    branch.Id,
                    requiredQuantityByProductId)
            })
            .Where(item => item.Availability.CanFulfillCart)
            .Select(item => item.Branch.Id)
            .FirstOrDefault();

        if (fulfillableBranch != Guid.Empty)
        {
            return fulfillableBranch;
        }

        return orderedCandidates
            .Select(branch => (Guid?)branch.Id)
            .FirstOrDefault();
    }

    private static async Task<List<VendorOfferSnapshot>> LoadCandidateOffersAsync(
        IApplicationDbContext context,
        IReadOnlyCollection<Guid> masterProductIds,
        Guid? selectedVendorId,
        CancellationToken cancellationToken)
    {
        return await context.VendorProducts
            .AsNoTracking()
            .Where(product =>
                masterProductIds.Contains(product.MasterProductId) &&
                (!selectedVendorId.HasValue || product.VendorId == selectedVendorId.Value) &&
                product.Status == VendorProductStatus.Active &&
                product.IsAvailable &&
                product.StockQuantity > 0 &&
                product.MasterProduct.Status == ProductStatus.Active &&
                product.Vendor.Status == VendorStatus.Active)
            .Select(product => new VendorOfferSnapshot(
                product.Id,
                product.VendorId,
                product.VendorBranchId,
                product.VendorBranch != null && product.VendorBranch.IsPrimary,
                product.MasterProductId,
                product.StockQuantity,
                product.SellingPrice,
                product.CreatedAtUtc,
                product.CustomNameAr,
                product.CustomNameEn,
                product.MasterProduct.NameAr,
                product.MasterProduct.NameEn,
                product.MasterProduct.Images
                    .OrderByDescending(image => image.IsPrimary)
                    .ThenBy(image => image.DisplayOrder)
                    .Select(image => image.Url)
                    .ToList(),
                MasterProductDisplayDto.BuildDisplaySize(
                    product.MasterProduct.PackageType != null ? product.MasterProduct.PackageType.NameAr : null,
                    product.MasterProduct.MeasurementValue,
                    product.MasterProduct.MeasurementUnit != null ? product.MasterProduct.MeasurementUnit.NameAr : product.MasterProduct.UnitOfMeasure != null ? product.MasterProduct.UnitOfMeasure.NameAr : null,
                    product.MasterProduct.MeasurementUnit != null ? product.MasterProduct.MeasurementUnit.Symbol : product.MasterProduct.UnitOfMeasure != null ? product.MasterProduct.UnitOfMeasure.Symbol : null,
                    true),
                MasterProductDisplayDto.BuildDisplaySize(
                    product.MasterProduct.PackageType != null ? product.MasterProduct.PackageType.NameEn : null,
                    product.MasterProduct.MeasurementValue,
                    product.MasterProduct.MeasurementUnit != null ? product.MasterProduct.MeasurementUnit.NameEn : product.MasterProduct.UnitOfMeasure != null ? product.MasterProduct.UnitOfMeasure.NameEn : null,
                    product.MasterProduct.MeasurementUnit != null ? product.MasterProduct.MeasurementUnit.Symbol : product.MasterProduct.UnitOfMeasure != null ? product.MasterProduct.UnitOfMeasure.Symbol : null,
                    false),
                product.MasterProduct.PackageType != null ? product.MasterProduct.PackageType.NameAr : null,
                product.MasterProduct.PackageType != null ? product.MasterProduct.PackageType.NameEn : null,
                product.MasterProduct.MeasurementValue,
                product.MasterProduct.MeasurementUnit != null ? product.MasterProduct.MeasurementUnit.NameAr : product.MasterProduct.UnitOfMeasure != null ? product.MasterProduct.UnitOfMeasure.NameAr : null,
                product.MasterProduct.MeasurementUnit != null ? product.MasterProduct.MeasurementUnit.NameEn : product.MasterProduct.UnitOfMeasure != null ? product.MasterProduct.UnitOfMeasure.NameEn : null,
                MasterProductDisplayDto.BuildLegacyUnit(
                    product.MasterProduct.PackageType != null ? product.MasterProduct.PackageType.NameAr : null,
                    product.MasterProduct.MeasurementUnit != null ? product.MasterProduct.MeasurementUnit.NameAr : product.MasterProduct.UnitOfMeasure != null ? product.MasterProduct.UnitOfMeasure.NameAr : null,
                    true),
                MasterProductDisplayDto.BuildLegacyUnit(
                    product.MasterProduct.PackageType != null ? product.MasterProduct.PackageType.NameEn : null,
                    product.MasterProduct.MeasurementUnit != null ? product.MasterProduct.MeasurementUnit.NameEn : product.MasterProduct.UnitOfMeasure != null ? product.MasterProduct.UnitOfMeasure.NameEn : null,
                    false)))
            .ToListAsync(cancellationToken);
    }

    private static (bool CanFulfillCart, int MissingItemsCount) EvaluateBranchCartAvailability(
        IReadOnlyList<VendorOfferSnapshot> offers,
        Guid vendorId,
        Guid branchId,
        IReadOnlyDictionary<Guid, int> requiredQuantityByProductId)
    {
        var missing = 0;
        foreach (var (productId, requiredQuantity) in requiredQuantityByProductId)
        {
            var productOffers = offers
                .Where(offer => offer.VendorId == vendorId && offer.MasterProductId == productId)
                .ToList();

            var branchOffers = productOffers
                .Where(offer => offer.VendorBranchId == branchId && offer.StockQuantity >= requiredQuantity)
                .ToList();
            if (branchOffers.Count > 0)
            {
                continue;
            }

            var hasBranchScopedInventory = productOffers.Any(offer => offer.VendorBranchId.HasValue);
            if (hasBranchScopedInventory)
            {
                missing++;
                continue;
            }

            var vendorWideOffer = productOffers.FirstOrDefault(offer =>
                !offer.VendorBranchId.HasValue && offer.StockQuantity >= requiredQuantity);
            if (vendorWideOffer is null)
            {
                missing++;
            }
        }

        return (missing == 0, missing);
    }

    public static CheckoutDeliveryCheckDto BuildPickupDeliveryCheck(bool canProceed, string? messageEn = null) =>
        new(
            canProceed ? "pickup_ready" : "pickup_branch_required",
            canProceed,
            canProceed,
            canProceed ? "يمكن متابعة الطلب للاستلام من الفرع." : "يرجى اختيار فرع الاستلام.",
            messageEn ?? (canProceed ? "Pickup checkout is ready." : "Please select a pickup branch."),
            0m,
            0m);

    public static List<CheckoutPaymentMethodDto> BuildPickupPaymentMethods(bool cardAvailable, bool cashOnPickupEnabled = false)
    {
        var methods = new List<CheckoutPaymentMethodDto>
        {
            new("card", "بطاقة ائتمان / مدى", "Credit / Debit Card", "فيزا، ماستركارد، مدى", "Visa, Mastercard, Mada", cardAvailable, cardAvailable),
            new("apple_pay", "Apple Pay", "Apple Pay", "دفع سريع وآمن", "Fast and secure payment", cardAvailable, false)
        };

        if (cashOnPickupEnabled)
        {
            methods.Add(new CheckoutPaymentMethodDto(
                "cash",
                "الدفع عند الاستلام",
                "Pay on Pickup",
                "ادفع نقدًا للتاجر عند استلام الطلب",
                "Pay cash to the merchant when collecting the order",
                true,
                true));
        }

        return methods;
    }

    public static CheckoutPickupBranchDto? BuildPickupBranchDto(VendorBranch? branch)
    {
        if (branch is null)
        {
            return null;
        }

        var city = ResolveBranchCity(branch);
        var region = ResolveBranchRegion(branch);
        var localizedCity = SaudiGeographyDisplay.LocalizeCity(city);
        var address = SaudiGeographyDisplay.FormatBranchAddress(
            branch.AddressLine,
            city,
            region);

        return new CheckoutPickupBranchDto(
            branch.Id,
            branch.Name,
            branch.AddressLine,
            localizedCity,
            address,
            BranchOperatingHoursSupport.BuildHoursTodayLabel(
                branch.OperatingHours?.ToList() ?? [],
                DateTime.UtcNow));
    }

    private static string ResolveBranchCity(VendorBranch branch) =>
        FirstNonBlank(branch.City, branch.Vendor?.City) ?? string.Empty;

    private static string ResolveBranchRegion(VendorBranch branch) =>
        FirstNonBlank(branch.Region, branch.Vendor?.Region) ?? string.Empty;

    private static string? FirstNonBlank(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    public static decimal CalculatePickupCommissionAmount(decimal subtotal, decimal pickupCommissionPercent) =>
        Math.Round(subtotal * pickupCommissionPercent / 100m, 2, MidpointRounding.AwayFromZero);
}
