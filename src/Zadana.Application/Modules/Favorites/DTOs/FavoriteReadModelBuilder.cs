using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Favorites.DTOs;
using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Vendors.Enums;

namespace Zadana.Application.Modules.Favorites.DTOs;

internal static class FavoriteReadModelBuilder
{
    public static async Task<Dictionary<Guid, FavoriteItemDto>> BuildAsync(
        IApplicationDbContext context,
        IEnumerable<Guid> masterProductIds,
        CancellationToken cancellationToken)
    {
        var requestedIds = masterProductIds.Distinct().ToArray();
        if (requestedIds.Length == 0)
        {
            return [];
        }

        var salesByVendorProductId = await context.OrderItems
            .AsNoTracking()
            .Where(item => item.Order.Status == OrderStatus.Delivered)
            .GroupBy(item => item.VendorProductId)
            .Select(group => new
            {
                VendorProductId = group.Key,
                Quantity = group.Sum(item => item.Quantity)
            })
            .ToDictionaryAsync(item => item.VendorProductId, item => item.Quantity, cancellationToken);

        var reviewStatsByVendorId = await context.Reviews
            .AsNoTracking()
            .GroupBy(review => review.VendorId)
            .Select(group => new
            {
                VendorId = group.Key,
                AverageRating = Math.Round(group.Average(review => review.Rating), 1),
                ReviewCount = group.Count()
            })
            .ToDictionaryAsync(
                item => item.VendorId,
                item => new VendorReviewStats((decimal)item.AverageRating, item.ReviewCount),
                cancellationToken);

        var offers = await context.VendorProducts
            .AsNoTracking()
            .Where(product =>
                requestedIds.Contains(product.MasterProductId) &&
                product.Status == VendorProductStatus.Active &&
                product.IsAvailable &&
                product.StockQuantity > 0 &&
                product.MasterProduct.Status == ProductStatus.Active &&
                product.Vendor.Status == VendorStatus.Active &&
                product.Vendor.AcceptOrders)
            .Select(product => new FavoriteOfferRow(
                product.Id,
                product.MasterProductId,
                product.VendorId,
                product.CreatedAtUtc,
                !string.IsNullOrWhiteSpace(product.CustomNameAr) ? product.CustomNameAr : product.MasterProduct.NameAr,
                !string.IsNullOrWhiteSpace(product.CustomNameEn) ? product.CustomNameEn : product.MasterProduct.NameEn,
                product.Vendor.BusinessNameAr,
                product.Vendor.BusinessNameEn,
                product.SellingPrice,
                product.CompareAtPrice,
                product.MasterProduct.UnitOfMeasure != null ? product.MasterProduct.UnitOfMeasure.NameAr : null,
                product.MasterProduct.UnitOfMeasure != null ? product.MasterProduct.UnitOfMeasure.NameEn : null,
                product.MasterProduct.Images
                    .OrderByDescending(image => image.IsPrimary)
                    .ThenBy(image => image.DisplayOrder)
                    .Select(image => image.Url)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);

        return offers
            .Select(offer =>
            {
                reviewStatsByVendorId.TryGetValue(offer.VendorId, out var reviewStats);
                salesByVendorProductId.TryGetValue(offer.VendorProductId, out var salesCount);

                return new
                {
                    offer.MasterProductId,
                    Offer = new FavoriteItemSource(
                        offer.MasterProductId,
                        offer.CreatedAtUtc,
                        FavoriteProjectionMapper.PickLocalized(offer.NameAr, offer.NameEn),
                        FavoriteProjectionMapper.PickLocalized(offer.StoreAr, offer.StoreEn),
                        offer.SellingPrice,
                        offer.CompareAtPrice,
                        FavoriteProjectionMapper.PickLocalizedNullable(offer.UnitAr, offer.UnitEn),
                        offer.ImageUrl,
                        reviewStats?.AverageRating,
                        reviewStats?.ReviewCount ?? 0),
                    SalesCount = salesCount
                };
            })
            .GroupBy(item => item.MasterProductId)
            .ToDictionary(
                group => group.Key,
                group => FavoriteProjectionMapper.Map(group
                    .OrderBy(item => item.Offer.SellingPrice)
                    .ThenByDescending(item => item.SalesCount)
                    .ThenByDescending(item => item.Offer.CreatedAtUtc)
                    .Select(item => item.Offer)
                    .First()));
    }

    private sealed record VendorReviewStats(decimal AverageRating, int ReviewCount);
}
