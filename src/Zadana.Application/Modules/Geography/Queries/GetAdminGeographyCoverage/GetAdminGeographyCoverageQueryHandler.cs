using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Caching;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Geography;
using Zadana.Application.Modules.Geography.DTOs;
using Zadana.Application.Modules.Geography.Support;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Vendors.Enums;

namespace Zadana.Application.Modules.Geography.Queries.GetAdminGeographyCoverage;

public sealed class GetAdminGeographyCoverageQueryHandler(
    IApplicationDbContext dbContext,
    IGeographyCityResolver cityResolver,
    IAppCache cache)
    : IRequestHandler<GetAdminGeographyCoverageQuery, AdminGeographyCoverageDto>
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(8);

    public async Task<AdminGeographyCoverageDto> Handle(
        GetAdminGeographyCoverageQuery request,
        CancellationToken cancellationToken)
    {
        var normalizedRegion = GeographyRegionFilter.Normalize(request.Region);

        var cacheKey = AppCacheKeys.Build(
            "admin",
            "geography",
            "coverage",
            AppCacheKeys.TextToken(normalizedRegion),
            AppCacheKeys.BoolToken(request.GapsOnly));

        return await cache.GetOrCreateAsync(
            cacheKey,
            async token => await BuildCoverageAsync(normalizedRegion, request.GapsOnly, token),
            new AppCacheEntryOptions(CacheDuration),
            cancellationToken: cancellationToken);
    }

    private async Task<AdminGeographyCoverageDto> BuildCoverageAsync(
        string normalizedRegion,
        bool gapsOnly,
        CancellationToken cancellationToken)
    {
        await cityResolver.RefreshCatalogAsync(cancellationToken);

        var masterCities = await dbContext.SaudiCities
            .AsNoTracking()
            .Include(city => city.Region)
            .OrderBy(city => city.Region.SortOrder)
            .ThenBy(city => city.SortOrder)
            .Select(city => new MasterCityRow(
                city.Code,
                city.Region.Code,
                city.Region.NameAr,
                city.Region.NameEn,
                city.NameAr,
                city.NameEn))
            .ToListAsync(cancellationToken);

        var cityRows = masterCities.ToDictionary(
            city => city.CityCode,
            city => new MutableCityRow(city));

        var customerCounts = new Dictionary<string, HashSet<Guid>>(StringComparer.Ordinal);
        var vendorCounts = new Dictionary<string, HashSet<Guid>>(StringComparer.Ordinal);
        var branchCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var readyDriverCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var verifiedDriverCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        var activeCustomerIds = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Role == UserRole.Customer && user.AccountStatus == AccountStatus.Active)
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);

        if (activeCustomerIds.Count > 0)
        {
            var addresses = await dbContext.CustomerAddresses
                .AsNoTracking()
                .Where(address => activeCustomerIds.Contains(address.UserId))
                .Select(address => new
                {
                    address.UserId,
                    address.City,
                    address.IsDefault,
                    address.CreatedAtUtc
                })
                .ToListAsync(cancellationToken);

            var cityByUser = addresses
                .GroupBy(address => address.UserId)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderByDescending(address => address.IsDefault)
                        .ThenByDescending(address => address.CreatedAtUtc)
                        .First()
                        .City);

            foreach (var userId in activeCustomerIds)
            {
                cityByUser.TryGetValue(userId, out var rawCity);
                var resolved = cityResolver.ResolveLocation(rawCity, null);
                var cityCode = resolved.CityCode ?? GeographyCoverageConstants.UnmappedCityCode;
                if (!customerCounts.TryGetValue(cityCode, out var users))
                {
                    users = [];
                    customerCounts[cityCode] = users;
                }

                users.Add(userId);
            }
        }

        var activeVendors = await dbContext.Vendors
            .AsNoTracking()
            .Where(vendor =>
                vendor.Status == VendorStatus.Active
                && vendor.AcceptOrders
                && vendor.LockedAtUtc == null)
            .Select(vendor => new { vendor.Id, vendor.City, vendor.Region })
            .ToListAsync(cancellationToken);

        var activeVendorIds = activeVendors.Select(vendor => vendor.Id).ToList();
        var activeBranches = await dbContext.VendorBranches
            .AsNoTracking()
            .Where(branch => branch.IsActive && activeVendorIds.Contains(branch.VendorId))
            .Select(branch => new { branch.VendorId, branch.City, branch.Region })
            .ToListAsync(cancellationToken);

        var branchesByVendor = activeBranches
            .GroupBy(branch => branch.VendorId)
            .ToDictionary(group => group.Key, group => group.ToList());

        foreach (var vendor in activeVendors)
        {
            if (branchesByVendor.TryGetValue(vendor.Id, out var branches) && branches.Count > 0)
            {
                foreach (var branch in branches)
                {
                    var resolved = cityResolver.ResolveLocation(branch.City, branch.Region);
                    if (!resolved.IsKnown)
                    {
                        continue;
                    }

                    AddVendor(branchCounts, vendorCounts, resolved.CityCode!, vendor.Id, countBranch: true);
                }
            }
            else
            {
                var resolved = cityResolver.ResolveLocation(vendor.City, vendor.Region);
                if (!resolved.IsKnown)
                {
                    continue;
                }

                AddVendor(branchCounts, vendorCounts, resolved.CityCode!, vendor.Id, countBranch: false);
            }
        }

        var drivers = await dbContext.Drivers
            .AsNoTracking()
            .Select(driver => new
            {
                driver.City,
                driver.Region,
                driver.Status,
                driver.VerificationStatus,
                driver.IsAvailable,
                driver.IsLocationUpdatesBlocked
            })
            .ToListAsync(cancellationToken);

        foreach (var driver in drivers)
        {
            var resolved = cityResolver.ResolveLocation(driver.City, driver.Region);
            if (!resolved.IsKnown)
            {
                continue;
            }

            var cityCode = resolved.CityCode!;

            if (AdminDriverReadiness.IsVerifiedActive(
                    driver.Status,
                    driver.VerificationStatus,
                    driver.IsLocationUpdatesBlocked))
            {
                verifiedDriverCounts.TryGetValue(cityCode, out var verifiedCount);
                verifiedDriverCounts[cityCode] = verifiedCount + 1;
            }

            if (AdminDriverReadiness.IsReady(
                    driver.Status,
                    driver.VerificationStatus,
                    driver.IsAvailable,
                    driver.IsLocationUpdatesBlocked))
            {
                readyDriverCounts.TryGetValue(cityCode, out var readyCount);
                readyDriverCounts[cityCode] = readyCount + 1;
            }
        }

        if (customerCounts.TryGetValue(GeographyCoverageConstants.UnmappedCityCode, out var unmappedUsers))
        {
            cityRows[GeographyCoverageConstants.UnmappedCityCode] = new MutableCityRow(
                new MasterCityRow(
                    GeographyCoverageConstants.UnmappedCityCode,
                    string.Empty,
                    "غير مصنّف",
                    "Unmapped",
                    "غير مصنّف",
                    "Unmapped"));
            cityRows[GeographyCoverageConstants.UnmappedCityCode].CustomerCount = unmappedUsers.Count;
        }

        foreach (var (cityCode, users) in customerCounts)
        {
            if (!cityRows.ContainsKey(cityCode))
            {
                continue;
            }

            cityRows[cityCode].CustomerCount = users.Count;
        }

        foreach (var (cityCode, vendors) in vendorCounts)
        {
            if (!cityRows.ContainsKey(cityCode))
            {
                continue;
            }

            cityRows[cityCode].ActiveVendorCount = vendors.Count;
        }

        foreach (var (cityCode, count) in branchCounts)
        {
            if (!cityRows.ContainsKey(cityCode))
            {
                continue;
            }

            cityRows[cityCode].ActiveBranchCount = count;
        }

        foreach (var (cityCode, count) in verifiedDriverCounts)
        {
            if (!cityRows.ContainsKey(cityCode))
            {
                continue;
            }

            cityRows[cityCode].VerifiedDriverCount = count;
        }

        foreach (var (cityCode, count) in readyDriverCounts)
        {
            if (!cityRows.ContainsKey(cityCode))
            {
                continue;
            }

            cityRows[cityCode].ReadyDriverCount = count;
        }

        var dtoCities = cityRows.Values
            .Select(row =>
            {
                var gapFlags = GeographyCoverageGapRules.BuildGapFlags(
                    row.CustomerCount,
                    row.ActiveVendorCount,
                    row.ReadyDriverCount,
                    row.VerifiedDriverCount);
                return new AdminGeographyCoverageCityDto
                {
                    CityCode = row.Master.CityCode,
                    RegionCode = row.Master.RegionCode,
                    CityNameAr = row.Master.CityNameAr,
                    CityNameEn = row.Master.CityNameEn,
                    CustomerCount = row.CustomerCount,
                    ActiveVendorCount = row.ActiveVendorCount,
                    ReadyDriverCount = row.ReadyDriverCount,
                    VerifiedDriverCount = row.VerifiedDriverCount,
                    ActiveBranchCount = row.ActiveBranchCount,
                    GapFlags = gapFlags,
                    Routes = BuildRoutes(row.Master)
                };
            })
            .Where(city => GeographyRegionFilter.MatchesCity(
                new AdminGeographyCoverageCityFilter(city.CityCode, city.RegionCode),
                normalizedRegion))
            .Where(city => !gapsOnly || GeographyCoverageGapRules.HasOperationalGap(city.GapFlags))
            .OrderBy(city => city.CityCode == GeographyCoverageConstants.UnmappedCityCode ? 1 : 0)
            .ThenByDescending(city => city.CustomerCount)
            .ThenByDescending(city => GeographyCoverageGapRules.GapSeverityScore(city.GapFlags))
            .ThenBy(city => city.CityNameEn)
            .ToList();

        var regionRollup = dtoCities
            .Where(city => city.CityCode != GeographyCoverageConstants.UnmappedCityCode)
            .GroupBy(city => city.RegionCode)
            .Select(group =>
            {
                var master = masterCities.First(city => city.RegionCode == group.Key);
                return new AdminGeographyCoverageRegionRollupDto
                {
                    RegionCode = group.Key,
                    RegionNameAr = master.RegionNameAr,
                    RegionNameEn = master.RegionNameEn,
                    CustomerCount = group.Sum(city => city.CustomerCount),
                    ActiveVendorCount = group.Sum(city => city.ActiveVendorCount),
                    ReadyDriverCount = group.Sum(city => city.ReadyDriverCount),
                    CitiesWithGaps = group.Count(city => GeographyCoverageGapRules.HasOperationalGap(city.GapFlags))
                };
            })
            .OrderByDescending(region => region.CustomerCount)
            .ToList();

        var citiesWithGaps = dtoCities.Count(city => GeographyCoverageGapRules.HasOperationalGap(city.GapFlags));
        var customersWithoutVendor = dtoCities
            .Where(city => city.GapFlags.Contains(GeographyCoverageConstants.GapFlags.NoVendor))
            .Sum(city => city.CustomerCount);
        var customersWithoutDriver = dtoCities
            .Where(city => city.GapFlags.Contains(GeographyCoverageConstants.GapFlags.NoDriver))
            .Sum(city => city.CustomerCount);
        var unmappedCustomers = dtoCities
            .FirstOrDefault(city => city.CityCode == GeographyCoverageConstants.UnmappedCityCode)
            ?.CustomerCount ?? 0;

        var officialCityCount = normalizedRegion == GeographyCoverageConstants.AllRegionsToken
            ? masterCities.Count
            : masterCities.Count(city =>
                string.Equals(city.RegionCode, normalizedRegion, StringComparison.OrdinalIgnoreCase));

        var topDemandGaps = dtoCities
            .Where(city => city.GapFlags.Count > 0 && city.CustomerCount > 0)
            .OrderByDescending(city => city.CustomerCount)
            .ThenByDescending(city => GeographyCoverageGapRules.GapSeverityScore(city.GapFlags))
            .Take(5)
            .Select(city => new AdminGeographyCoverageTopGapDto
            {
                CityCode = city.CityCode,
                CityNameAr = city.CityNameAr,
                CityNameEn = city.CityNameEn,
                CustomerCount = city.CustomerCount,
                GapFlags = city.GapFlags
            })
            .ToList();

        return new AdminGeographyCoverageDto
        {
            Summary = new AdminGeographyCoverageSummaryDto
            {
                OfficialCityCount = officialCityCount,
                CitiesWithGaps = citiesWithGaps,
                CustomersWithoutVendor = customersWithoutVendor,
                CustomersWithoutDriver = customersWithoutDriver,
                UnmappedCustomers = unmappedCustomers,
                TopDemandGaps = topDemandGaps
            },
            Cities = dtoCities,
            RegionRollup = regionRollup
        };
    }

    private static void AddVendor(
        Dictionary<string, int> branchCounts,
        Dictionary<string, HashSet<Guid>> vendorCounts,
        string cityCode,
        Guid vendorId,
        bool countBranch)
    {
        if (!vendorCounts.TryGetValue(cityCode, out var vendors))
        {
            vendors = [];
            vendorCounts[cityCode] = vendors;
        }

        if (vendors.Add(vendorId) && countBranch)
        {
            branchCounts.TryGetValue(cityCode, out var branches);
            branchCounts[cityCode] = branches + 1;
        }
        else if (countBranch)
        {
            branchCounts.TryGetValue(cityCode, out var branches);
            branchCounts[cityCode] = branches + 1;
        }
    }

    private static AdminGeographyCoverageRoutesDto BuildRoutes(MasterCityRow city)
    {
        var customerCity = Uri.EscapeDataString(city.CityNameAr);
        var vendorCityCode = Uri.EscapeDataString(city.CityCode);
        var driverCity = Uri.EscapeDataString(city.CityCode);

        return new AdminGeographyCoverageRoutesDto
        {
            Customers = $"/customers?city={customerCity}",
            Vendors = $"/vendors?cityCode={vendorCityCode}",
            Drivers = $"/drivers?city={driverCity}"
        };
    }

    private sealed record MasterCityRow(
        string CityCode,
        string RegionCode,
        string RegionNameAr,
        string RegionNameEn,
        string CityNameAr,
        string CityNameEn);

    private sealed class MutableCityRow(MasterCityRow master)
    {
        public MasterCityRow Master { get; } = master;
        public int CustomerCount { get; set; }
        public int ActiveVendorCount { get; set; }
        public int ReadyDriverCount { get; set; }
        public int VerifiedDriverCount { get; set; }
        public int ActiveBranchCount { get; set; }
    }
}
