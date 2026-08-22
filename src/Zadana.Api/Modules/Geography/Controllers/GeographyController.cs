using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Zadana.Api.Controllers;
using Zadana.Api.Configuration;
using Zadana.Application.Common.Caching;
using Zadana.Application.Common.Interfaces;
using Zadana.Infrastructure.Persistence;

namespace Zadana.Api.Modules.Geography.Controllers;

[Route("api/geography")]
[AllowAnonymous]
[Tags("Geography")]
public class GeographyController : ApiControllerBase
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);
    private readonly ApplicationDbContext _dbContext;
    private readonly IAppCache _cache;

    public GeographyController(ApplicationDbContext dbContext, IAppCache cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    [HttpGet("regions")]
    [OutputCache(PolicyName = OutputCachePolicyNames.Geography)]
    public Task<IReadOnlyList<SaudiRegionLookupDto>> GetRegions(CancellationToken cancellationToken)
    {
        return _cache.GetOrCreateAsync<IReadOnlyList<SaudiRegionLookupDto>>(
            AppCacheKeys.Build("geography", "saudi-regions", AppCacheKeys.CurrentCulture),
            async token => await _dbContext.SaudiRegions
                .AsNoTracking()
                .OrderBy(region => region.SortOrder)
                .ThenBy(region => region.NameEn)
                .Select(region => new SaudiRegionLookupDto(
                    region.Code,
                    region.NameAr,
                    region.NameEn,
                    region.Latitude,
                    region.Longitude,
                    region.MapZoom,
                    region.SortOrder,
                    region.IsOperational))
                .ToListAsync(token),
            new AppCacheEntryOptions(CacheDuration),
            cancellationToken: cancellationToken);
    }

    [HttpGet("regions/{regionCode}/cities")]
    [OutputCache(PolicyName = OutputCachePolicyNames.Geography)]
    public Task<IReadOnlyList<SaudiCityLookupDto>> GetCities(string regionCode, CancellationToken cancellationToken)
    {
        var normalizedRegionCode = regionCode.Trim().ToUpperInvariant();

        return _cache.GetOrCreateAsync<IReadOnlyList<SaudiCityLookupDto>>(
            AppCacheKeys.Build("geography", "saudi-cities", normalizedRegionCode, AppCacheKeys.CurrentCulture),
            async token => await _dbContext.SaudiCities
                .AsNoTracking()
                .Where(city => city.Region.Code == normalizedRegionCode)
                .OrderBy(city => city.SortOrder)
                .ThenBy(city => city.NameEn)
                .Select(city => new SaudiCityLookupDto(
                    city.Region.Code,
                    city.Code,
                    city.NameAr,
                    city.NameEn,
                    city.Latitude,
                    city.Longitude,
                    city.MapZoom,
                    city.SortOrder,
                    city.IsOperational && city.Region.IsOperational))
                .ToListAsync(token),
            new AppCacheEntryOptions(CacheDuration),
            cancellationToken: cancellationToken);
    }

    [HttpGet("driver/regions")]
    public async Task<IReadOnlyList<SaudiDriverRegionLookupDto>> GetDriverRegions(CancellationToken cancellationToken)
    {
        return await _dbContext.SaudiRegions
            .AsNoTracking()
            .OrderBy(region => region.SortOrder)
            .ThenBy(region => region.NameEn)
            .Select(region => new SaudiDriverRegionLookupDto(
                region.Code,
                region.NameAr,
                region.NameEn,
                region.Latitude,
                region.Longitude,
                region.MapZoom,
                region.SortOrder,
                region.IsOperational))
            .ToListAsync(cancellationToken);
    }

    [HttpGet("driver/regions/{regionCode}/cities")]
    public async Task<IReadOnlyList<SaudiCityLookupDto>> GetDriverCities(
        string regionCode,
        CancellationToken cancellationToken)
    {
        var normalizedRegionCode = regionCode.Trim().ToUpperInvariant();

        return await _dbContext.SaudiCities
            .AsNoTracking()
            .Where(city => city.Region.Code == normalizedRegionCode)
            .OrderBy(city => city.SortOrder)
            .ThenBy(city => city.NameEn)
            .Select(city => new SaudiCityLookupDto(
                city.Region.Code,
                city.Code,
                city.NameAr,
                city.NameEn,
                city.Latitude,
                city.Longitude,
                city.MapZoom,
                city.SortOrder,
                city.IsOperational && city.Region.IsOperational))
            .ToListAsync(cancellationToken);
    }
}

public sealed record SaudiRegionLookupDto(
    string Code,
    string NameAr,
    string NameEn,
    double Latitude,
    double Longitude,
    int MapZoom,
    int SortOrder,
    bool IsOperational);

public sealed record SaudiDriverRegionLookupDto(
    string Code,
    string NameAr,
    string NameEn,
    double Latitude,
    double Longitude,
    int MapZoom,
    int SortOrder,
    bool IsOperational);

public sealed record SaudiCityLookupDto(
    string RegionCode,
    string Code,
    string NameAr,
    string NameEn,
    double Latitude,
    double Longitude,
    int MapZoom,
    int SortOrder,
    bool IsOperational);
