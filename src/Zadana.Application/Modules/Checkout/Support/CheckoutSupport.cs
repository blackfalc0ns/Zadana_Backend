using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Checkout.DTOs;
using Zadana.Application.Modules.Orders.Support;
using Zadana.Domain.Modules.Catalog.Enums;
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
        CancellationToken cancellationToken)
    {
        var masterProductIds = cart.Items.Select(x => x.MasterProductId).Distinct().ToList();
        var visibleOffers = await context.VendorProducts
            .AsNoTracking()
            .Where(product =>
                masterProductIds.Contains(product.MasterProductId) &&
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

        var candidateVendor = visibleOffers
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

        if (candidateVendor == null)
        {
            throw new BusinessRuleException("CHECKOUT_VENDOR_UNAVAILABLE", "No single vendor can fulfill all cart items for checkout.");
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
        Cart cart,
        CancellationToken cancellationToken)
    {
        if (!cart.CouponId.HasValue)
        {
            return null;
        }

        return await context.Coupons
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == cart.CouponId.Value, cancellationToken);
    }

    public static async Task<Coupon> ResolveCouponByCodeAsync(
        IApplicationDbContext context,
        string code,
        Guid vendorId,
        decimal subtotal,
        CancellationToken cancellationToken)
    {
        var coupon = await context.Coupons
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Code == code.Trim().ToUpperInvariant(), cancellationToken)
            ?? throw new BusinessRuleException("INVALID_PROMO_CODE", "Promo code is invalid.");

        if (!coupon.IsValid())
        {
            throw new BusinessRuleException("INVALID_PROMO_CODE", "Promo code is invalid or inactive.");
        }

        if (coupon.MinOrderAmount.HasValue && subtotal < coupon.MinOrderAmount.Value)
        {
            throw new BusinessRuleException("PROMO_MIN_ORDER_NOT_MET", "Promo code minimum order amount is not met.");
        }

        var hasRestrictions = await context.CouponVendors
            .AsNoTracking()
            .AnyAsync(x => x.CouponId == coupon.Id, cancellationToken);

        if (hasRestrictions)
        {
            var isApplicable = await context.CouponVendors
                .AsNoTracking()
                .AnyAsync(x => x.CouponId == coupon.Id && x.VendorId == vendorId, cancellationToken);
            if (!isApplicable)
            {
                throw new BusinessRuleException("PROMO_CODE_NOT_APPLICABLE", "Promo code is not applicable to the selected vendor.");
            }
        }

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
        new(subtotal, shippingCost, discount, Math.Max(0m, subtotal + shippingCost - discount), Currency);

    public static List<CheckoutDeliverySlotDto> BuildDeliverySlots(string? selectedSlotId)
    {
        var selected = string.IsNullOrWhiteSpace(selectedSlotId) ? DefaultDeliverySlotId : selectedSlotId.Trim();
        return
        [
            new CheckoutDeliverySlotDto(
                DefaultDeliverySlotId,
                "30-45 minutes",
                DateTime.UtcNow.AddMinutes(30),
                DateTime.UtcNow.AddMinutes(45),
                true,
                string.Equals(selected, DefaultDeliverySlotId, StringComparison.OrdinalIgnoreCase))
        ];
    }

    public static List<CheckoutPaymentMethodDto> BuildPaymentMethods(bool cardAvailable) =>
    [
        new CheckoutPaymentMethodDto("card", "Credit / Debit Card", cardAvailable, cardAvailable),
        new CheckoutPaymentMethodDto("apple_pay", "Apple Pay", false, false),
        new CheckoutPaymentMethodDto("cash", "Cash on Delivery", true, !cardAvailable),
        new CheckoutPaymentMethodDto("bank", "Bank Transfer", true, false)
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

    public static string MapPaymentMethodCodeToEnumName(string paymentMethodCode) =>
        paymentMethodCode.Trim().ToLowerInvariant() switch
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

    internal sealed record CheckoutPricingSnapshot(
        Guid VendorId,
        Guid? VendorBranchId,
        List<CheckoutCartItemDto> Items,
        decimal Subtotal);

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
}
