using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Application.Modules.Orders.DTOs;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Application.Modules.Vendors.Support;
using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Vendors.Enums;

namespace Zadana.Application.Modules.Orders.Support;

internal static class CartProjection
{
    public static async Task<CartDto> BuildCartDtoAsync(
        IApplicationDbContext context,
        Cart? cart,
        CancellationToken cancellationToken,
        Guid? selectedVendorId = null,
        CustomerAddress? address = null,
        bool preferCheapestVendorWhenAmbiguous = false)
    {
        if (cart is null || cart.Items.Count == 0)
        {
            return new CartDto([], new CartSummaryDto(0, 0, null, null, null));
        }

        var masterProductIds = cart.Items
            .Select(item => item.MasterProductId)
            .Distinct()
            .ToList();
        var requiredQuantityByProductId = cart.Items
            .GroupBy(item => item.MasterProductId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));

        var masterProducts = await context.MasterProducts
            .AsNoTracking()
            .Where(product => masterProductIds.Contains(product.Id))
            .Select(product => new MasterProductSnapshot(
                product.Id,
                product.NameAr,
                product.NameEn,
                product.Images
                    .OrderByDescending(image => image.IsPrimary)
                    .ThenBy(image => image.DisplayOrder)
                    .Select(image => image.Url)
                    .ToList(),
                MasterProductDisplayDto.BuildDisplaySize(
                    product.PackageType != null ? product.PackageType.NameAr : null,
                    product.MeasurementValue,
                    product.MeasurementUnit != null ? product.MeasurementUnit.NameAr : product.UnitOfMeasure != null ? product.UnitOfMeasure.NameAr : null,
                    product.MeasurementUnit != null ? product.MeasurementUnit.Symbol : product.UnitOfMeasure != null ? product.UnitOfMeasure.Symbol : null,
                    true),
                MasterProductDisplayDto.BuildDisplaySize(
                    product.PackageType != null ? product.PackageType.NameEn : null,
                    product.MeasurementValue,
                    product.MeasurementUnit != null ? product.MeasurementUnit.NameEn : product.UnitOfMeasure != null ? product.UnitOfMeasure.NameEn : null,
                    product.MeasurementUnit != null ? product.MeasurementUnit.Symbol : product.UnitOfMeasure != null ? product.UnitOfMeasure.Symbol : null,
                    false),
                product.PackageType != null ? product.PackageType.NameAr : null,
                product.PackageType != null ? product.PackageType.NameEn : null,
                product.MeasurementValue,
                product.MeasurementUnit != null ? product.MeasurementUnit.NameAr : product.UnitOfMeasure != null ? product.UnitOfMeasure.NameAr : null,
                product.MeasurementUnit != null ? product.MeasurementUnit.NameEn : product.UnitOfMeasure != null ? product.UnitOfMeasure.NameEn : null,
                MasterProductDisplayDto.BuildLegacyUnit(
                    product.PackageType != null ? product.PackageType.NameAr : null,
                    product.MeasurementUnit != null ? product.MeasurementUnit.NameAr : product.UnitOfMeasure != null ? product.UnitOfMeasure.NameAr : null,
                    true),
                MasterProductDisplayDto.BuildLegacyUnit(
                    product.PackageType != null ? product.PackageType.NameEn : null,
                    product.MeasurementUnit != null ? product.MeasurementUnit.NameEn : product.UnitOfMeasure != null ? product.UnitOfMeasure.NameEn : null,
                    false)))
            .ToDictionaryAsync(product => product.Id, cancellationToken);

        var candidateOffers = await context.VendorProducts
            .AsNoTracking()
            .Where(product =>
                masterProductIds.Contains(product.MasterProductId) &&
                product.Status == VendorProductStatus.Active &&
                product.IsAvailable &&
                product.StockQuantity > 0 &&
                product.MasterProduct.Status == ProductStatus.Active &&
                product.Vendor.Status == VendorStatus.Active)
            .Select(product => new VisibleCartOfferSnapshot(
                product.Id,
                product.VendorId,
                product.VendorBranchId,
                product.VendorBranch != null && product.VendorBranch.IsPrimary,
                product.MasterProductId,
                product.StockQuantity,
                product.SellingPrice,
                product.CompareAtPrice,
                product.CreatedAtUtc,
                product.Vendor.BusinessNameAr,
                product.Vendor.BusinessNameEn,
                product.MasterProduct.Images
                    .OrderByDescending(image => image.IsPrimary)
                    .ThenBy(image => image.DisplayOrder)
                    .Select(image => image.Url)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);

        candidateOffers = ApplyUnifiedPricing(candidateOffers);

        var selectedBranchIdByVendor = await CartBranchSelectionSupport.ResolveAddressBranchIdsByVendorAsync(
            context,
            candidateOffers.Select(offer => offer.VendorId),
            address,
            cancellationToken);

        candidateOffers = FilterOffersForAddressBranch(candidateOffers, selectedBranchIdByVendor);

        var availabilityVendorIds = selectedVendorId.HasValue
            ? candidateOffers.Select(offer => offer.VendorId).Append(selectedVendorId.Value)
            : candidateOffers.Select(offer => offer.VendorId);
        var availabilityDecisions = await VendorCustomerAvailabilityPolicy.LoadDecisionsAsync(
            context,
            availabilityVendorIds,
            cancellationToken);

        var visibleOffers = candidateOffers
            .Where(offer => VendorCustomerAvailabilityPolicy.ResolveOrOffline(availabilityDecisions, offer.VendorId).IsVisibleInCatalog)
            .ToList();
        var purchasableVisibleOffers = visibleOffers
            .Where(offer =>
                requiredQuantityByProductId.TryGetValue(offer.MasterProductId, out var requiredQuantity) &&
                offer.StockQuantity >= requiredQuantity)
            .ToList();

        var effectiveVendorId = ResolveEffectiveVendorId(
            selectedVendorId,
            masterProductIds,
            purchasableVisibleOffers,
            cart.Items,
            preferCheapestVendorWhenAmbiguous);
        var selectedVendorDecision = effectiveVendorId.HasValue
            ? VendorCustomerAvailabilityPolicy.ResolveOrOffline(availabilityDecisions, effectiveVendorId.Value)
            : null;

        var scopedVisibleOffers = effectiveVendorId.HasValue
            ? purchasableVisibleOffers.Where(offer => offer.VendorId == effectiveVendorId.Value)
            : purchasableVisibleOffers;

        var offersByProductId = scopedVisibleOffers
            .GroupBy(offer => offer.MasterProductId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(offer => offer.Price)
                    .ThenByDescending(offer => offer.CreatedAtUtc)
                    .ThenBy(offer => PickLocalized(offer.StoreAr, offer.StoreEn), StringComparer.CurrentCultureIgnoreCase)
                    .ToList());

        var items = cart.Items
            .OrderBy(item => item.CreatedAtUtc)
            .ThenBy(item => item.ProductName, StringComparer.CurrentCultureIgnoreCase)
            .Select(item =>
            {
                masterProducts.TryGetValue(item.MasterProductId, out var product);
                offersByProductId.TryGetValue(item.MasterProductId, out var offers);
                var hasCandidateAtSelectedVendor = !effectiveVendorId.HasValue || candidateOffers.Any(offer =>
                    offer.VendorId == effectiveVendorId.Value &&
                    offer.MasterProductId == item.MasterProductId);
                var hasInsufficientStockAtSelectedVendor = effectiveVendorId.HasValue && visibleOffers.Any(offer =>
                    offer.VendorId == effectiveVendorId.Value &&
                    offer.MasterProductId == item.MasterProductId &&
                    offer.StockQuantity < item.Quantity);
                var isAvailable = !effectiveVendorId.HasValue || (offers?.Count > 0);
                var availabilityStatus = isAvailable
                    ? null
                    : effectiveVendorId.HasValue && selectedVendorDecision is not null && !selectedVendorDecision.IsVisibleInCatalog && hasCandidateAtSelectedVendor
                        ? selectedVendorDecision.ReasonCode
                        : hasInsufficientStockAtSelectedVendor
                            ? "insufficient_stock"
                        : "unavailable_at_selected_vendor";

                var vendorPrices = offers?
                    .Select(offer => new CartVendorPriceDto(
                        offer.Id,
                        PickLocalized(offer.StoreAr, offer.StoreEn),
                        offer.Price,
                        IsDiscounted(offer.Price, offer.OldPrice) ? offer.OldPrice : null,
                        IsDiscounted(offer.Price, offer.OldPrice)))
                    .ToList() ?? [];

                return new CartItemDto(
                    item.Id,
                    item.MasterProductId,
                    product is null ? item.ProductName : PickLocalized(product.NameAr, product.NameEn),
                    offers?.FirstOrDefault()?.ImageUrl ?? product?.ImageUrl,
                    product is null ? null : PickLocalizedNullable(product.UnitAr, product.UnitEn),
                    item.Quantity,
                    vendorPrices,
                    isAvailable,
                    availabilityStatus,
                    product is null ? null : PickLocalizedNullable(product.DisplaySizeAr, product.DisplaySizeEn),
                    product is null ? null : PickLocalizedNullable(product.PackageTypeAr, product.PackageTypeEn),
                    product?.MeasurementValue,
                    product is null ? null : PickLocalizedNullable(product.MeasurementUnitAr, product.MeasurementUnitEn),
                    offers?.FirstOrDefault()?.ImageUrl ?? product?.ImageUrl,
                    product?.ImageUrls);
            })
            .ToList();

        decimal? subtotal = null;
        decimal? discountAmount = null;
        decimal? totalAmount = null;
        var checkoutBlockReason = default(string);
        var canCheckout = false;

        if (effectiveVendorId.HasValue)
        {
            var subtotalValue = 0m;
            var discountAmountValue = 0m;
            var totalAmountValue = 0m;
            var pricedItemsCount = 0;

            foreach (var cartItem in cart.Items)
            {
                if (!offersByProductId.TryGetValue(cartItem.MasterProductId, out var offers) || offers.Count == 0)
                {
                    continue;
                }

                var selectedOffer = offers[0];
                var originalUnitPrice = IsDiscounted(selectedOffer.Price, selectedOffer.OldPrice)
                    ? selectedOffer.OldPrice!.Value
                    : selectedOffer.Price;
                var lineSubtotal = originalUnitPrice * cartItem.Quantity;
                var lineTotal = selectedOffer.Price * cartItem.Quantity;

                subtotalValue += lineSubtotal;
                totalAmountValue += lineTotal;
                discountAmountValue += lineSubtotal - lineTotal;
                pricedItemsCount++;
            }

            if (pricedItemsCount > 0)
            {
                subtotal = subtotalValue;
                discountAmount = discountAmountValue;
                totalAmount = totalAmountValue;
            }
        }

        var unavailableItemsCount = items.Count(item => !item.IsAvailable);
        var requiresUnavailableItemsConfirmation = false;
        if (effectiveVendorId.HasValue)
        {
            var hasPurchasableItems = items.Any(item => item.IsAvailable) && totalAmount.HasValue;
            canCheckout = (selectedVendorDecision?.IsPurchasable ?? true) && hasPurchasableItems;
            requiresUnavailableItemsConfirmation = canCheckout && unavailableItemsCount > 0;
            checkoutBlockReason = canCheckout
                ? null
                : selectedVendorDecision is not null && !selectedVendorDecision.IsPurchasable
                    ? selectedVendorDecision.ReasonCode
                : unavailableItemsCount > 0 && !hasPurchasableItems
                    ? "cart_contains_unavailable_items"
                    : "pricing_unavailable_for_selected_vendor";
        }

        return new CartDto(
            items,
            new CartSummaryDto(
                items.Count,
                items.Sum(item => item.Quantity),
                subtotal,
                discountAmount,
                totalAmount,
                totalAmount.HasValue,
                canCheckout,
                checkoutBlockReason,
                unavailableItemsCount > 0,
                unavailableItemsCount,
                requiresUnavailableItemsConfirmation),
            items.Count,
            items.Count,
            0,
            false);
    }

    private static Guid? ResolveEffectiveVendorId(
        Guid? selectedVendorId,
        IReadOnlyCollection<Guid> masterProductIds,
        IReadOnlyCollection<VisibleCartOfferSnapshot> visibleOffers,
        IEnumerable<CartItem> cartItems,
        bool preferCheapestWhenAmbiguous = false)
    {
        if (selectedVendorId.HasValue)
        {
            return selectedVendorId;
        }

        if (masterProductIds.Count == 0 || visibleOffers.Count == 0 || !cartItems.Any())
        {
            return null;
        }

        var qualifyingVendorGroups = visibleOffers
            .GroupBy(offer => offer.VendorId)
            .Where(group => group
                .Select(offer => offer.MasterProductId)
                .Distinct()
                .Count() == masterProductIds.Count)
            .ToList();

        if (qualifyingVendorGroups.Count == 0)
        {
            return null;
        }

        if (qualifyingVendorGroups.Count == 1)
        {
            return qualifyingVendorGroups[0].Key;
        }

        if (!preferCheapestWhenAmbiguous)
        {
            return null;
        }

        var quantityByProductId = cartItems
            .GroupBy(item => item.MasterProductId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));

        Guid? cheapestVendorId = null;
        decimal? cheapestTotal = null;

        foreach (var vendorGroup in qualifyingVendorGroups)
        {
            var bestOfferByProductId = vendorGroup
                .GroupBy(offer => offer.MasterProductId)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderBy(offer => offer.Price)
                        .ThenByDescending(offer => offer.CreatedAtUtc)
                        .First());

            var vendorTotal = 0m;
            var coversAllProducts = true;

            foreach (var masterProductId in masterProductIds)
            {
                if (!bestOfferByProductId.TryGetValue(masterProductId, out var offer))
                {
                    coversAllProducts = false;
                    break;
                }

                vendorTotal += offer.Price * quantityByProductId[masterProductId];
            }

            if (!coversAllProducts)
            {
                continue;
            }

            if (!cheapestTotal.HasValue || vendorTotal < cheapestTotal.Value)
            {
                cheapestTotal = vendorTotal;
                cheapestVendorId = vendorGroup.Key;
            }
        }

        return cheapestVendorId;
    }

    private static List<VisibleCartOfferSnapshot> ApplyUnifiedPricing(List<VisibleCartOfferSnapshot> offers)
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
                    .First());

        return offers
            .Select(offer =>
            {
                var canonical = canonicalPricingByProduct[new { offer.VendorId, offer.MasterProductId }];
                return offer.Price == canonical.Price && offer.OldPrice == canonical.OldPrice
                    ? offer
                    : offer with
                    {
                        Price = canonical.Price,
                        OldPrice = canonical.OldPrice
                    };
            })
            .ToList();
    }

    private static List<VisibleCartOfferSnapshot> FilterOffersForAddressBranch(
        List<VisibleCartOfferSnapshot> offers,
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
                // Cart store switching should reflect the vendor's catalog inventory, not
                // make the item disappear just because the customer's current/default
                // address cannot resolve to a branch. Delivery and branch reachability are
                // validated later by delivery-check/checkout.
                if (!selectedBranchIdByVendor.TryGetValue(group.Key.VendorId, out var selectedBranchId) ||
                    !selectedBranchId.HasValue)
                {
                    return group.AsEnumerable();
                }

                var branchOffers = group
                    .Where(offer => offer.VendorBranchId == selectedBranchId.Value)
                    .ToList();

                if (branchOffers.Count > 0)
                {
                    return branchOffers;
                }

                var vendorWideOffers = group
                    .Where(offer => !offer.VendorBranchId.HasValue)
                    .ToList();

                return vendorWideOffers.Count > 0
                    ? vendorWideOffers
                    : group.ToList();
            })
            .ToList();
    }

    public static async Task<CartSummaryDto> BuildCartSummaryForMutationAsync(
        IApplicationDbContext context,
        Cart cart,
        Guid? selectedVendorId,
        CustomerAddress? address,
        CancellationToken cancellationToken)
    {
        if (cart.Items.Count == 0)
        {
            return new CartSummaryDto(0, 0, null, null, null);
        }

        if (selectedVendorId.HasValue)
        {
            var selectedSummary = await TryBuildPricingSummaryAsync(
                context,
                cart,
                selectedVendorId,
                address,
                cancellationToken);
            if (selectedSummary is not null)
            {
                return selectedSummary;
            }
        }

        var autoSummary = await TryBuildPricingSummaryAsync(context, cart, null, address, cancellationToken)
            ?? await TryBuildPricingSummaryAsync(context, cart, null, address, cancellationToken, preferCheapestVendorWhenAmbiguous: true);
        if (autoSummary is not null)
        {
            return autoSummary;
        }

        var singleProductVendorId = await ResolveSingleProductPricingVendorIdAsync(context, cart, cancellationToken);
        if (singleProductVendorId.HasValue)
        {
            var singleProductSummary = await TryBuildPricingSummaryAsync(
                context,
                cart,
                singleProductVendorId,
                address,
                cancellationToken);
            if (singleProductSummary is not null)
            {
                return singleProductSummary;
            }
        }

        var partialSummary = await ResolveBestPartialCartSummaryAsync(context, cart, address, cancellationToken);
        if (partialSummary is not null)
        {
            return partialSummary;
        }

        return (await BuildCartDtoAsync(context, cart, cancellationToken, selectedVendorId, address)).Summary;
    }

    private static async Task<CartSummaryDto?> TryBuildPricingSummaryAsync(
        IApplicationDbContext context,
        Cart cart,
        Guid? vendorId,
        CustomerAddress? address,
        CancellationToken cancellationToken,
        bool preferCheapestVendorWhenAmbiguous = false)
    {
        var dto = await BuildCartDtoAsync(
            context,
            cart,
            cancellationToken,
            vendorId,
            address,
            preferCheapestVendorWhenAmbiguous);

        return dto.Summary.IsPricingAvailable ? dto.Summary : null;
    }

    private static async Task<CartSummaryDto?> ResolveBestPartialCartSummaryAsync(
        IApplicationDbContext context,
        Cart cart,
        CustomerAddress? address,
        CancellationToken cancellationToken)
    {
        var masterProductIds = cart.Items
            .Select(item => item.MasterProductId)
            .Distinct()
            .ToList();

        if (masterProductIds.Count == 0)
        {
            return null;
        }

        var vendorIds = await context.VendorProducts
            .AsNoTracking()
            .Where(product =>
                masterProductIds.Contains(product.MasterProductId) &&
                product.Status == VendorProductStatus.Active &&
                product.IsAvailable &&
                product.StockQuantity > 0 &&
                product.MasterProduct.Status == ProductStatus.Active &&
                product.Vendor.Status == VendorStatus.Active)
            .Select(product => product.VendorId)
            .Distinct()
            .ToListAsync(cancellationToken);

        CartSummaryDto? bestSummary = null;
        decimal bestTotal = -1m;

        foreach (var vendorId in vendorIds)
        {
            var dto = await BuildCartDtoAsync(context, cart, cancellationToken, vendorId, address);
            if (!dto.Summary.IsPricingAvailable || !dto.Summary.TotalAmount.HasValue)
            {
                continue;
            }

            if (dto.Summary.TotalAmount.Value > bestTotal)
            {
                bestTotal = dto.Summary.TotalAmount.Value;
                bestSummary = dto.Summary;
            }
        }

        return bestSummary;
    }

    public static async Task<Guid?> ResolveSingleProductPricingVendorIdAsync(
        IApplicationDbContext context,
        Cart cart,
        CancellationToken cancellationToken)
    {
        var masterProductIds = cart.Items
            .Select(item => item.MasterProductId)
            .Distinct()
            .ToArray();

        if (masterProductIds.Length != 1)
        {
            return null;
        }

        var offers = await context.VendorProducts
            .AsNoTracking()
            .Where(product =>
                product.MasterProductId == masterProductIds[0] &&
                product.Status == VendorProductStatus.Active &&
                product.IsAvailable &&
                product.StockQuantity > 0 &&
                product.MasterProduct.Status == ProductStatus.Active &&
                product.Vendor.Status == VendorStatus.Active)
            .Select(product => new
            {
                product.VendorId,
                Price = product.SellingPrice,
                product.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        if (offers.Count == 0)
        {
            return null;
        }

        var decisions = await VendorCustomerAvailabilityPolicy.LoadDecisionsAsync(
            context,
            offers.Select(offer => offer.VendorId),
            cancellationToken);

        return offers
            .Where(offer => VendorCustomerAvailabilityPolicy.ResolveOrOffline(decisions, offer.VendorId).IsVisibleInCatalog)
            .OrderBy(offer => offer.Price)
            .ThenByDescending(offer => offer.CreatedAtUtc)
            .ThenBy(offer => offer.VendorId)
            .Select(offer => (Guid?)offer.VendorId)
            .FirstOrDefault();
    }

    public static Task<bool> HasVisibleOfferAsync(
        IApplicationDbContext context,
        Guid masterProductId,
        Guid? vendorId,
        CancellationToken cancellationToken)
    {
        return HasVisibleOfferInternalAsync(context, masterProductId, vendorId, cancellationToken);
    }

    private static async Task<bool> HasVisibleOfferInternalAsync(
        IApplicationDbContext context,
        Guid masterProductId,
        Guid? vendorId,
        CancellationToken cancellationToken)
    {
        var offers = await context.VendorProducts
            .AsNoTracking()
            .Where(product =>
                product.MasterProductId == masterProductId &&
                (!vendorId.HasValue || product.VendorId == vendorId.Value) &&
                product.Status == VendorProductStatus.Active &&
                product.IsAvailable &&
                product.StockQuantity > 0 &&
                product.MasterProduct.Status == ProductStatus.Active &&
                product.Vendor.Status == VendorStatus.Active)
            .Select(product => new { product.VendorId })
            .ToListAsync(cancellationToken);

        if (offers.Count == 0)
        {
            return false;
        }

        var decisions = await VendorCustomerAvailabilityPolicy.LoadDecisionsAsync(
            context,
            offers.Select(offer => offer.VendorId),
            cancellationToken);

        return offers.Any(offer => VendorCustomerAvailabilityPolicy.ResolveOrOffline(decisions, offer.VendorId).IsVisibleInCatalog);
    }

    private static bool IsArabic() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase);

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

    private static bool IsDiscounted(decimal price, decimal? oldPrice) =>
        oldPrice.HasValue && oldPrice.Value > price;

    private sealed record MasterProductSnapshot(
        Guid Id,
        string NameAr,
        string NameEn,
        List<string> ImageUrls,
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
        public string? ImageUrl => ImageUrls.FirstOrDefault();
    }

    private sealed record VisibleCartOfferSnapshot(
        Guid Id,
        Guid VendorId,
        Guid? VendorBranchId,
        bool IsPrimaryBranch,
        Guid MasterProductId,
        int StockQuantity,
        decimal Price,
        decimal? OldPrice,
        DateTime CreatedAtUtc,
        string StoreAr,
        string StoreEn,
        string? ImageUrl);
}
