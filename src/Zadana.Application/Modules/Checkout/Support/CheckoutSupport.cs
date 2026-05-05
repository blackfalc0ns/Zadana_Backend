using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Checkout.DTOs;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Orders.Support;
using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Marketing.Entities;
using Zadana.Domain.Modules.Marketing.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Checkout.Support;

internal static class CheckoutSupport
{
    public const string DefaultDeliverySlotId = "standard-30-45";
    public const string Currency = "EGP";

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
        CancellationToken cancellationToken)
    {
        var masterProductIds = cart.Items.Select(x => x.MasterProductId).Distinct().ToList();
        var visibleOffers = await context.VendorProducts
            .AsNoTracking()
            .Where(product =>
                masterProductIds.Contains(product.MasterProductId) &&
                (!selectedVendorId.HasValue || product.VendorId == selectedVendorId.Value) &&
                product.Status == VendorProductStatus.Active &&
                product.IsAvailable &&
                product.StockQuantity > 0 &&
                product.MasterProduct.Status == ProductStatus.Active &&
                product.Vendor.Status == VendorStatus.Active &&
                product.Vendor.AcceptOrders)
            .Select(product => new VendorOfferSnapshot(
                product.Id,
                product.VendorId,
                product.VendorBranchId,
                product.MasterProductId,
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
                    .FirstOrDefault(),
                product.MasterProduct.UnitOfMeasure != null ? product.MasterProduct.UnitOfMeasure.NameAr : null,
                product.MasterProduct.UnitOfMeasure != null ? product.MasterProduct.UnitOfMeasure.NameEn : null))
            .ToListAsync(cancellationToken);

        CandidateVendorSnapshot? candidateVendor;
        if (selectedVendorId.HasValue)
        {
            var offers = visibleOffers
                .Where(x => x.VendorId == selectedVendorId.Value)
                .GroupBy(x => x.MasterProductId)
                .Select(offerGroup => offerGroup
                    .OrderBy(x => x.Price)
                    .ThenByDescending(x => x.CreatedAtUtc)
                    .First())
                .ToList();

            candidateVendor = offers.Count == masterProductIds.Count
                ? new CandidateVendorSnapshot(
                    selectedVendorId.Value,
                    true,
                    offers.Sum(chosen => chosen.Price * cart.Items.First(item => item.MasterProductId == chosen.MasterProductId).Quantity),
                    offers)
                : null;
        }
        else
        {
            candidateVendor = visibleOffers
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
                ? new BusinessRuleException("VENDOR_MISSING_CART_PRODUCT", "The selected vendor does not offer all products currently in the cart.")
                : new BusinessRuleException("CHECKOUT_VENDOR_UNAVAILABLE", "No single vendor can fulfill all cart items for checkout.");
        }

        var items = cart.Items
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.ProductName, StringComparer.CurrentCultureIgnoreCase)
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
                    offer.Price * item.Quantity);
            })
            .ToList();

        return new CheckoutPricingSnapshot(
            candidateVendor.VendorId,
            ResolveSingleBranchId(candidateVendor.Offers),
            items,
            items.Sum(x => x.TotalPrice));
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
        CancellationToken cancellationToken)
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

        return await deliveryPricingService.QuoteAsync(vendorBranchId.Value, address.Id, cancellationToken);
    }

    public static DeliveryPriceQuote BuildNoPricingQuote() =>
        new(0m, 0m, 0m, 0m, 0m, "zone-fallback", "No pricing");

    public static CheckoutDeliveryQuoteDto BuildDeliveryQuoteDto(DeliveryPriceQuote quote) =>
        new(
            quote.DistanceKm,
            quote.BaseFee,
            quote.DistanceFee,
            quote.SurgeFee,
            quote.TotalFee,
            quote.PricingMode,
            quote.RuleLabel);

    public static async Task<CheckoutFinanceBreakdown> ResolveFinanceBreakdownAsync(
        IApplicationDbContext context,
        CustomerAddress? address,
        decimal subtotal,
        decimal shippingCost,
        decimal discount,
        string? paymentMethodCode,
        CancellationToken cancellationToken)
    {
        var settings = await ResolveZoneFinanceSettingsAsync(context, address, cancellationToken);
        var normalizedPaymentMethod = NormalizePaymentMethodCode(paymentMethodCode);
        var taxableBase = Math.Max(0m, subtotal + shippingCost - discount);

        var vatAmount = settings.IsVatActive && settings.VatPercent > 0m
            ? decimal.Round(taxableBase * settings.VatPercent / 100m, 2, MidpointRounding.AwayFromZero)
            : 0m;

        var codFee = normalizedPaymentMethod == "cash" && settings.IsCodFeeActive
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
        new CheckoutPaymentMethodDto("apple_pay", "Apple Pay", "Apple Pay", "دفع سريع وآمن", "Fast and secure payment", false, false),
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
            "card" or "credit_card" or "creditcard" or "debit_card" or "debitcard" => "card",
            "cash" or "cash_on_delivery" or "cashondelivery" or "cod" => "cash",
            "bank" or "bank_transfer" or "banktransfer" => "bank",
            "apple_pay" or "applepay" => "apple_pay",
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

    private static Guid? ResolveSingleBranchId(IReadOnlyCollection<VendorOfferSnapshot> offers)
    {
        var branchIds = offers.Select(x => x.VendorBranchId).Distinct().ToList();
        return branchIds.Count == 1 ? branchIds[0] : null;
    }

    private static string PickLocalized(string? arabic, string? english) =>
        !string.IsNullOrWhiteSpace(arabic) ? arabic.Trim() : english?.Trim() ?? string.Empty;

    private static string? PickLocalizedNullable(string? arabic, string? english)
    {
        var value = PickLocalized(arabic, english);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

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

    internal sealed record CheckoutPricingSnapshot(
        Guid VendorId,
        Guid? VendorBranchId,
        List<CheckoutCartItemDto> Items,
        decimal Subtotal);

    internal sealed record CheckoutFinanceBreakdown(
        decimal TaxableBase,
        decimal VatAmount,
        decimal CodFee,
        CheckoutTotalsDto Totals);

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
        Guid MasterProductId,
        decimal Price,
        DateTime CreatedAtUtc,
        string? CustomNameAr,
        string? CustomNameEn,
        string NameAr,
        string NameEn,
        string? ImageUrl,
        string? UnitAr,
        string? UnitEn);

    private sealed record CandidateVendorSnapshot(
        Guid VendorId,
        bool CoversAllProducts,
        decimal Total,
        List<VendorOfferSnapshot> Offers);

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
}
