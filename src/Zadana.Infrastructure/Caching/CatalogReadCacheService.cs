using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Zadana.Application.Common.Caching;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Settings;
using Zadana.Application.Modules.Catalog.Interfaces;
using Zadana.Domain.Modules.Orders.Enums;

namespace Zadana.Infrastructure.Caching;

public sealed class CatalogReadCacheService(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService,
    IAppCache cache,
    IOptions<CachingSettings> cachingOptions) : ICatalogReadCacheService
{
    private readonly CacheDurationSettings _durations = cachingOptions.Value.Durations;

    public async Task<IReadOnlyDictionary<Guid, int>> GetDeliveredSalesByVendorProductIdAsync(
        CancellationToken cancellationToken = default)
    {
        var key = AppCacheKeys.Build("catalog", "stats", "sales", "v1");
        var result = await cache.GetOrCreateAsync(
            key,
            async token =>
                await dbContext.OrderItems
                    .AsNoTracking()
                    .Where(item => item.Order.Status == OrderStatus.Delivered)
                    .GroupBy(item => item.VendorProductId)
                    .ToDictionaryAsync(group => group.Key, group => group.Sum(item => item.Quantity), token),
            CreateOptions(_durations.BrowseBase),
            [CacheTagNames.Catalog],
            cancellationToken);

        return result;
    }

    public async Task<IReadOnlyDictionary<Guid, VendorReviewStatsSnapshot>> GetVendorReviewStatsByVendorIdAsync(
        CancellationToken cancellationToken = default)
    {
        var key = AppCacheKeys.Build("catalog", "stats", "vendor-reviews", "v1");
        var result = await cache.GetOrCreateAsync(
            key,
            async token =>
                await dbContext.Reviews
                    .AsNoTracking()
                    .GroupBy(review => review.VendorId)
                    .ToDictionaryAsync(
                        group => group.Key,
                        group => new VendorReviewStatsSnapshot(
                            (decimal)Math.Round(group.Average(review => review.Rating), 1),
                            group.Count()),
                        token),
            CreateOptions(_durations.BrowseBase),
            [CacheTagNames.Catalog],
            cancellationToken);

        return result;
    }

    public Task<IReadOnlySet<Guid>> GetCurrentFavoriteMasterProductIdsAsync(CancellationToken cancellationToken = default) =>
        GetFavoriteMasterProductIdsAsync(currentUserService.UserId, currentUserService.GuestDeviceId, cancellationToken);

    public async Task<IReadOnlySet<Guid>> GetFavoriteMasterProductIdsAsync(
        Guid? userId,
        string? guestDeviceId,
        CancellationToken cancellationToken = default)
    {
        if (!userId.HasValue && string.IsNullOrWhiteSpace(guestDeviceId))
        {
            return new HashSet<Guid>();
        }

        var scope = AppCacheKeys.ScopeToken(userId, guestDeviceId);
        var key = AppCacheKeys.Build("favorites", "set", "v1", scope);
        var tag = AppCacheKeys.FavoriteScopeTag(userId, guestDeviceId);
        var result = await cache.GetOrCreateAsync(
            key,
            async token =>
                await dbContext.CustomerFavorites
                    .AsNoTracking()
                    .Where(favorite =>
                        (userId.HasValue && favorite.UserId == userId.Value) ||
                        (!userId.HasValue && favorite.GuestId == guestDeviceId))
                    .Select(favorite => favorite.MasterProductId)
                    .ToArrayAsync(token),
            CreateOptions(_durations.FavoriteSet),
            [tag],
            cancellationToken);

        return result.ToHashSet();
    }

    public Task<CatalogPurchaseProfileSnapshot> GetPurchaseProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var key = AppCacheKeys.Build("home", "purchase-profile", "v2", $"user:{userId:N}");
        return cache.GetOrCreateAsync(
            key,
            async token =>
            {
                var purchases = await dbContext.OrderItems
                    .AsNoTracking()
                    .Where(item => item.Order.UserId == userId && item.Order.Status == OrderStatus.Delivered)
                    .Join(
                        dbContext.MasterProducts.AsNoTracking(),
                        orderItem => orderItem.MasterProductId,
                        masterProduct => masterProduct.Id,
                        (orderItem, masterProduct) => new
                        {
                            orderItem.MasterProductId,
                            orderItem.Quantity,
                            masterProduct.CategoryId,
                            masterProduct.BrandId
                        })
                    .ToListAsync(token);

                return new CatalogPurchaseProfileSnapshot(
                    purchases
                        .GroupBy(item => item.CategoryId)
                        .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity)),
                    purchases
                        .Where(item => item.BrandId.HasValue)
                        .GroupBy(item => item.BrandId!.Value)
                        .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity)),
                    purchases.Select(item => item.MasterProductId).ToHashSet());
            },
            CreateOptions(_durations.PurchaseProfile),
            [AppCacheKeys.PurchaseProfileTag(userId)],
            cancellationToken).ContinueWith(
                task => NormalizePurchaseProfile(task.Result),
                cancellationToken,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
    }

    private static AppCacheEntryOptions CreateOptions(TimeSpan duration) =>
        new(duration, duration);

    private static CatalogPurchaseProfileSnapshot NormalizePurchaseProfile(CatalogPurchaseProfileSnapshot? snapshot) =>
        new(
            snapshot?.CategoryScores ?? new Dictionary<Guid, int>(),
            snapshot?.BrandScores ?? new Dictionary<Guid, int>(),
            snapshot?.PurchasedMasterProductIds ?? new HashSet<Guid>());

}
