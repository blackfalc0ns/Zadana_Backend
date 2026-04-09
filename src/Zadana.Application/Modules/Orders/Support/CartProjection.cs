using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Orders.DTOs;
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
        Guid? selectedVendorId = null)
    {
        if (cart is null || cart.Items.Count == 0)
        {
            return new CartDto([], new CartSummaryDto(0, 0));
        }

        var masterProductIds = cart.Items
            .Select(item => item.MasterProductId)
            .Distinct()
            .ToList();

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
                    .FirstOrDefault(),
                product.UnitOfMeasure != null ? product.UnitOfMeasure.NameAr : null,
                product.UnitOfMeasure != null ? product.UnitOfMeasure.NameEn : null))
            .ToDictionaryAsync(product => product.Id, cancellationToken);

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
            .Select(product => new VisibleCartOfferSnapshot(
                product.Id,
                product.VendorId,
                product.MasterProductId,
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

        var offersByProductId = visibleOffers
            .Where(offer => selectedVendorId.HasValue && offer.VendorId == selectedVendorId.Value)
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
                    vendorPrices);
            })
            .ToList();

        return new CartDto(
            items,
            new CartSummaryDto(items.Count, items.Sum(item => item.Quantity)));
    }

    public static Task<bool> HasVisibleOfferAsync(
        IApplicationDbContext context,
        Guid masterProductId,
        Guid? vendorId,
        CancellationToken cancellationToken)
    {
        return context.VendorProducts
            .AsNoTracking()
            .AnyAsync(product =>
                product.MasterProductId == masterProductId &&
                (!vendorId.HasValue || product.VendorId == vendorId.Value) &&
                product.Status == VendorProductStatus.Active &&
                product.IsAvailable &&
                product.StockQuantity > 0 &&
                product.MasterProduct.Status == ProductStatus.Active &&
                product.Vendor.Status == VendorStatus.Active &&
                product.Vendor.AcceptOrders,
                cancellationToken);
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

    private static bool IsDiscounted(decimal price, decimal? oldPrice) =>
        oldPrice.HasValue && oldPrice.Value > price;

    private sealed record MasterProductSnapshot(Guid Id, string NameAr, string NameEn, string? ImageUrl, string? UnitAr, string? UnitEn);

    private sealed record VisibleCartOfferSnapshot(
        Guid Id,
        Guid VendorId,
        Guid MasterProductId,
        decimal Price,
        decimal? OldPrice,
        DateTime CreatedAtUtc,
        string StoreAr,
        string StoreEn,
        string? ImageUrl);
}
