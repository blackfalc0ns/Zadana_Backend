using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Zadana.Api.Controllers;
using Zadana.Infrastructure.Persistence;

namespace Zadana.Api.Modules.Geography.Controllers;

[Route("api/geography")]
[AllowAnonymous]
[Tags("Geography")]
public class GeographyController : ApiControllerBase
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);
    private readonly ApplicationDbContext _dbContext;
    private readonly IMemoryCache _cache;

    public GeographyController(ApplicationDbContext dbContext, IMemoryCache cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    [HttpGet("regions")]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
    public Task<IReadOnlyList<SaudiRegionLookupDto>> GetRegions(CancellationToken cancellationToken)
    {
        return _cache.GetOrCreateAsync<IReadOnlyList<SaudiRegionLookupDto>>("geography:saudi-regions", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;

            return await _dbContext.SaudiRegions
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
                    region.SortOrder))
                .ToListAsync(cancellationToken);
        })!;
    }

    [HttpGet("regions/{regionCode}/cities")]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any, VaryByQueryKeys = new[] { "regionCode" })]
    public Task<IReadOnlyList<SaudiCityLookupDto>> GetCities(string regionCode, CancellationToken cancellationToken)
    {
        var normalizedRegionCode = regionCode.Trim().ToUpperInvariant();

        return _cache.GetOrCreateAsync<IReadOnlyList<SaudiCityLookupDto>>($"geography:saudi-cities:{normalizedRegionCode}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;

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
                    city.SortOrder))
                .ToListAsync(cancellationToken);
        })!;
    }
}

public sealed record SaudiRegionLookupDto(
    string Code,
    string NameAr,
    string NameEn,
    double Latitude,
    double Longitude,
    int MapZoom,
    int SortOrder);

public sealed record SaudiCityLookupDto(
    string RegionCode,
    string Code,
    string NameAr,
    string NameEn,
    double Latitude,
    double Longitude,
    int MapZoom,
    int SortOrder);
