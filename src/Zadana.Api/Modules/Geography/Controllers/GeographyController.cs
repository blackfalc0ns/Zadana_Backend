using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Zadana.Api.Controllers;
using Zadana.Api.Configuration;
using Zadana.Application.Common.Caching;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Geography.Support;
using Zadana.Domain.Modules.Vendors.Enums;
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
                    region.SortOrder))
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
                    city.SortOrder))
                .ToListAsync(token),
            new AppCacheEntryOptions(CacheDuration),
            cancellationToken: cancellationToken);
    }

    [HttpGet("driver/regions/{regionCode}/cities")]
    public async Task<IReadOnlyList<SaudiCityLookupDto>> GetDriverCities(string regionCode, CancellationToken cancellationToken)
    {
        var normalizedRegionCode = regionCode.Trim().ToUpperInvariant();
        if (normalizedRegionCode != OperationalGeographyScope.EasternRegionCode)
        {
            return [];
        }

        return await _dbContext.SaudiCities
            .AsNoTracking()
            .Where(city => city.Region.Code == normalizedRegionCode)
            .Where(city => _dbContext.VendorBranches
                .AsNoTracking()
                .Any(branch =>
                    branch.IsActive
                    && branch.Vendor.Status == VendorStatus.Active
                    && branch.Vendor.AcceptOrders
                    && branch.Vendor.LockedAtUtc == null
                    && (
                        branch.City == city.Code
                        || branch.City == city.NameAr
                        || branch.City == city.NameEn
                        || (
                            branch.City == string.Empty
                            && (
                                branch.Vendor.City == city.Code
                                || branch.Vendor.City == city.NameAr
                                || branch.Vendor.City == city.NameEn)))))
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
