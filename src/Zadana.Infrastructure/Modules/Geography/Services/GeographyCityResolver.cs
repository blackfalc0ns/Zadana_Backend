using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Geography;
using Zadana.Infrastructure.Persistence;

namespace Zadana.Infrastructure.Modules.Geography.Services;

public sealed class GeographyCityResolver : IGeographyCityResolver
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private volatile GeographyCityCatalog? _catalog;

    public GeographyCityResolver(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public ResolvedCity Resolve(string? rawCity)
    {
        var catalog = EnsureCatalogLoaded();
        if (string.IsNullOrWhiteSpace(rawCity))
        {
            return ResolvedCity.Unknown;
        }

        var trimmed = rawCity.Trim();
        if (catalog.ByCode.TryGetValue(trimmed.ToUpperInvariant(), out var byCode))
        {
            return byCode with { MatchQuality = GeographyCityMatchQuality.ExactCode };
        }

        var normalized = GeographyCityNormalization.NormalizeCityName(trimmed);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return ResolvedCity.Unknown;
        }

        var aliasKey = GeographyCityAliases.MapAliasKey(normalized) ?? normalized;
        if (catalog.Lookup.TryGetValue(aliasKey, out var byAlias))
        {
            return byAlias with { MatchQuality = GeographyCityMatchQuality.Alias };
        }

        if (catalog.Lookup.TryGetValue(normalized, out var byName))
        {
            return byName with { MatchQuality = GeographyCityMatchQuality.ExactName };
        }

        return ResolvedCity.Unknown;
    }

    public ResolvedCity ResolveLocation(string? city, string? region)
    {
        var fromCity = Resolve(city);
        if (fromCity.IsKnown)
        {
            return fromCity;
        }

        var fromRegion = Resolve(region);
        if (fromRegion.IsKnown)
        {
            return fromRegion;
        }

        if (!string.IsNullOrWhiteSpace(city) && !string.IsNullOrWhiteSpace(region))
        {
            var combined = Resolve($"{city} {region}");
            if (combined.IsKnown)
            {
                return combined;
            }
        }

        return ResolvedCity.Unknown;
    }

    public async Task RefreshCatalogAsync(CancellationToken cancellationToken = default)
    {
        await _loadLock.WaitAsync(cancellationToken);
        try
        {
            _catalog = await LoadCatalogAsync(cancellationToken);
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private GeographyCityCatalog EnsureCatalogLoaded()
    {
        var catalog = _catalog;
        if (catalog is not null)
        {
            return catalog;
        }

        _loadLock.Wait();
        try
        {
            catalog = _catalog ?? LoadCatalogAsync(CancellationToken.None).GetAwaiter().GetResult();
            _catalog = catalog;
            return catalog;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private async Task<GeographyCityCatalog> LoadCatalogAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var cities = await dbContext.SaudiCities
            .AsNoTracking()
            .Include(city => city.Region)
            .OrderBy(city => city.SortOrder)
            .Select(city => new CitySeed(
                city.Code,
                city.Region.Code,
                city.NameAr,
                city.NameEn))
            .ToListAsync(cancellationToken);

        var lookup = new Dictionary<string, ResolvedCity>(StringComparer.Ordinal);
        var byCode = new Dictionary<string, ResolvedCity>(StringComparer.Ordinal);

        foreach (var city in cities)
        {
            var resolved = new ResolvedCity(
                city.Code,
                city.RegionCode,
                city.NameAr,
                city.NameEn,
                GeographyCityMatchQuality.ExactName);

            byCode[city.Code] = resolved;
            RegisterKey(lookup, GeographyCityNormalization.NormalizeCityName(city.Code), resolved);
            RegisterKey(lookup, GeographyCityNormalization.NormalizeCityName(city.NameAr), resolved);
            RegisterKey(lookup, GeographyCityNormalization.NormalizeCityName(city.NameEn), resolved);
        }

        foreach (var (aliasKey, cityCode) in GeographyCityAliases.ExplicitAliasCityCodes)
        {
            if (!byCode.TryGetValue(cityCode, out var resolved))
            {
                continue;
            }

            RegisterKey(lookup, aliasKey, resolved with { MatchQuality = GeographyCityMatchQuality.Alias });
        }

        return new GeographyCityCatalog(lookup, byCode);
    }

    private static void RegisterKey(Dictionary<string, ResolvedCity> lookup, string? key, ResolvedCity resolved)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        lookup.TryAdd(key, resolved);
    }

    private sealed record CitySeed(string Code, string RegionCode, string NameAr, string NameEn);

    private sealed record GeographyCityCatalog(
        Dictionary<string, ResolvedCity> Lookup,
        Dictionary<string, ResolvedCity> ByCode);
}
