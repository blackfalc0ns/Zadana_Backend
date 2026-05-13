namespace Zadana.Application.Modules.Catalog.Interfaces;

public interface ICatalogReadCacheService
{
    Task<IReadOnlyDictionary<Guid, int>> GetDeliveredSalesByVendorProductIdAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, VendorReviewStatsSnapshot>> GetVendorReviewStatsByVendorIdAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlySet<Guid>> GetCurrentFavoriteMasterProductIdsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlySet<Guid>> GetFavoriteMasterProductIdsAsync(
        Guid? userId,
        string? guestDeviceId,
        CancellationToken cancellationToken = default);

    Task<CatalogPurchaseProfileSnapshot> GetPurchaseProfileAsync(Guid userId, CancellationToken cancellationToken = default);
}

public sealed record VendorReviewStatsSnapshot(decimal AverageRating, int ReviewCount);

public sealed record CatalogPurchaseProfileSnapshot(
    IReadOnlyDictionary<Guid, int> CategoryScores,
    IReadOnlyDictionary<Guid, int> BrandScores,
    IReadOnlySet<Guid> PurchasedMasterProductIds);
