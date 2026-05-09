using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Zadana.Application.Common.Caching;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Settings;
using Zadana.Application.Modules.Catalog.Interfaces;
using Zadana.Domain.Modules.Orders.Enums;

namespace Zadana.UnitTests.Common;

internal static class TestServiceFactory
{
    public static IAppCache CreateAppCache() => new PassthroughAppCache();

    public static IOptions<CachingSettings> CreateCachingOptions() =>
        Options.Create(new CachingSettings());

    public static ICatalogReadCacheService CreateCatalogReadCacheService(
        IApplicationDbContext context,
        ICurrentUserService? currentUserService = null) =>
        new TestCatalogReadCacheService(
            context,
            currentUserService ?? new FakeCurrentUserService());

    public static ICacheInvalidator CreateCacheInvalidator() => new NoOpCacheInvalidator();

    private sealed class PassthroughAppCache : IAppCache
    {
        public Task<T> GetOrCreateAsync<T>(
            string key,
            Func<CancellationToken, Task<T>> factory,
            AppCacheEntryOptions options,
            IEnumerable<string>? tags = null,
            CancellationToken cancellationToken = default) =>
            factory(cancellationToken);
    }

    private sealed class NoOpCacheInvalidator : ICacheInvalidator
    {
        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RemoveByTagAsync(string tag, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RemoveByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class TestCatalogReadCacheService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService) : ICatalogReadCacheService
    {
        public async Task<IReadOnlyDictionary<Guid, int>> GetDeliveredSalesByVendorProductIdAsync(
            CancellationToken cancellationToken = default)
        {
            var rows = await context.OrderItems
                .AsNoTracking()
                .Where(item => item.Order.Status == OrderStatus.Delivered)
                .Select(item => new { item.VendorProductId, item.Quantity })
                .ToListAsync(cancellationToken);

            return rows
                .GroupBy(row => row.VendorProductId)
                .ToDictionary(group => group.Key, group => group.Sum(row => row.Quantity));
        }

        public async Task<IReadOnlyDictionary<Guid, VendorReviewStatsSnapshot>> GetVendorReviewStatsByVendorIdAsync(
            CancellationToken cancellationToken = default)
        {
            var rows = await context.Reviews
                .AsNoTracking()
                .Select(review => new { review.VendorId, review.Rating })
                .ToListAsync(cancellationToken);

            return rows
                .GroupBy(row => row.VendorId)
                .ToDictionary(
                    group => group.Key,
                    group => new VendorReviewStatsSnapshot(
                        (decimal)Math.Round(group.Average(row => row.Rating), 1),
                        group.Count()));
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

            var ids = await context.CustomerFavorites
                .AsNoTracking()
                .Where(favorite =>
                    (userId.HasValue && favorite.UserId == userId.Value) ||
                    (!userId.HasValue && favorite.GuestId == guestDeviceId))
                .Select(favorite => favorite.MasterProductId)
                .ToListAsync(cancellationToken);

            return ids.ToHashSet();
        }

        public async Task<CatalogPurchaseProfileSnapshot> GetPurchaseProfileAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var purchases = await context.OrderItems
                .AsNoTracking()
                .Where(item => item.Order.UserId == userId && item.Order.Status == OrderStatus.Delivered)
                .Join(
                    context.MasterProducts.AsNoTracking(),
                    orderItem => orderItem.MasterProductId,
                    masterProduct => masterProduct.Id,
                    (orderItem, masterProduct) => new { masterProduct.CategoryId, masterProduct.BrandId })
                .ToListAsync(cancellationToken);

            return new CatalogPurchaseProfileSnapshot(
                purchases.Select(item => item.CategoryId).Distinct().ToArray(),
                purchases.Where(item => item.BrandId.HasValue)
                    .Select(item => item.BrandId!.Value)
                    .Distinct()
                    .ToArray());
        }
    }
}
