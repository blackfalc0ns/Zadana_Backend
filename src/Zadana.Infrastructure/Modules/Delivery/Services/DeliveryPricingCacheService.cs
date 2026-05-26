using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Finances.Entities;
using Zadana.Domain.Modules.Geography.Entities;

namespace Zadana.Infrastructure.Modules.Delivery.Services;

/// <summary>
/// In-memory cache for delivery pricing reference data that rarely changes.
/// Reduces database load by caching pricing rules, zones, cities, and settings
/// with a short TTL (5 minutes). Data is refreshed automatically on expiry.
/// </summary>
public sealed class DeliveryPricingCacheService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private const string PricingRulesKey = "delivery:pricing-rules";
    private const string ZoneFinanceSettingsKey = "delivery:zone-finance-settings";
    private const string CityPricingSettingsKey = "delivery:city-pricing-settings";
    private const string RegionPricingSettingsKey = "delivery:region-pricing-settings";
    private const string DeliveryDefaultsKey = "delivery:pricing-defaults";
    private const string CitiesKey = "delivery:saudi-cities";
    private const string ActiveZonesKey = "delivery:active-zones";

    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;

    public DeliveryPricingCacheService(IMemoryCache cache, IServiceScopeFactory scopeFactory)
    {
        _cache = cache;
        _scopeFactory = scopeFactory;
    }

    public async Task<IReadOnlyList<DeliveryPricingRule>> GetPricingRulesAsync(CancellationToken cancellationToken = default)
    {
        return await _cache.GetOrCreateAsync(PricingRulesKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            await using var scope = _scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            return await context.DeliveryPricingRules
                .AsNoTracking()
                .Include(item => item.DeliveryZone)
                .Include(item => item.SurgeWindows)
                .Where(item => item.IsActive)
                .OrderByDescending(item => item.DeliveryZoneId != null)
                .ToListAsync(cancellationToken);
        }) ?? [];
    }

    public async Task<IReadOnlyDictionary<Guid, ZoneFinanceSettings>> GetZoneFinanceSettingsAsync(CancellationToken cancellationToken = default)
    {
        return await _cache.GetOrCreateAsync(ZoneFinanceSettingsKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            await using var scope = _scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            return (IReadOnlyDictionary<Guid, ZoneFinanceSettings>)await context.ZoneFinanceSettings
                .AsNoTracking()
                .ToDictionaryAsync(item => item.DeliveryZoneId, cancellationToken);
        }) ?? new Dictionary<Guid, ZoneFinanceSettings>();
    }

    public async Task<IReadOnlyList<CityDeliveryPricingSettings>> GetCityPricingSettingsAsync(CancellationToken cancellationToken = default)
    {
        return await _cache.GetOrCreateAsync(CityPricingSettingsKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            await using var scope = _scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            return await context.CityDeliveryPricingSettings
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }) ?? [];
    }

    public async Task<IReadOnlyList<RegionDeliveryPricingSettings>> GetRegionPricingSettingsAsync(CancellationToken cancellationToken = default)
    {
        return await _cache.GetOrCreateAsync(RegionPricingSettingsKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            await using var scope = _scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            return await context.RegionDeliveryPricingSettings
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }) ?? [];
    }

    public async Task<DeliveryPricingDefaults?> GetDeliveryDefaultsAsync(CancellationToken cancellationToken = default)
    {
        return await _cache.GetOrCreateAsync(DeliveryDefaultsKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            await using var scope = _scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            return await context.DeliveryPricingDefaults
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken);
        });
    }

    public async Task<IReadOnlyList<SaudiCity>> GetCitiesAsync(CancellationToken cancellationToken = default)
    {
        return await _cache.GetOrCreateAsync(CitiesKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            await using var scope = _scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            return await context.SaudiCities
                .AsNoTracking()
                .Include(item => item.Region)
                .ToListAsync(cancellationToken);
        }) ?? [];
    }

    public async Task<IReadOnlyList<DeliveryZone>> GetActiveZonesAsync(CancellationToken cancellationToken = default)
    {
        return await _cache.GetOrCreateAsync(ActiveZonesKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            await using var scope = _scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            return await context.DeliveryZones
                .AsNoTracking()
                .Where(zone => zone.IsActive)
                .ToListAsync(cancellationToken);
        }) ?? [];
    }

    /// <summary>
    /// Invalidates all cached pricing data. Call this when pricing rules
    /// are updated via the admin panel.
    /// </summary>
    public void InvalidateAll()
    {
        _cache.Remove(PricingRulesKey);
        _cache.Remove(ZoneFinanceSettingsKey);
        _cache.Remove(CityPricingSettingsKey);
        _cache.Remove(RegionPricingSettingsKey);
        _cache.Remove(DeliveryDefaultsKey);
        _cache.Remove(CitiesKey);
        _cache.Remove(ActiveZonesKey);
    }
}
