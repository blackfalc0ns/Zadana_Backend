using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Geography;

namespace Zadana.Application.Modules.Dashboard;

public sealed class DashboardGeographyScope
{
    public const string UnmappedRegionCode = "UNMAPPED";

    private readonly IGeographyCityResolver _resolver;
    private readonly IReadOnlyDictionary<string, SaudiRegionRow> _regionsByCode;
    private readonly IReadOnlyList<SaudiRegionRow> _regionsOrdered;

    public DashboardGeographyScope(
        IGeographyCityResolver resolver,
        IEnumerable<SaudiRegionRow> regions)
    {
        _resolver = resolver;
        _regionsOrdered = regions
            .OrderBy(region => region.SortOrder)
            .ThenBy(region => region.NameEn)
            .ToList();
        _regionsByCode = _regionsOrdered.ToDictionary(
            region => region.Code,
            StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<SaudiRegionRow> Regions => _regionsOrdered;

    public static string NormalizeFilterRegionToken(string? region) =>
        GeographyRegionFilter.Normalize(MapLegacyRegionCode(region));

    public string NormalizeFilterRegion(string? region) => NormalizeFilterRegionToken(region);

    public string ResolveEntityRegionCode(string? city, string? region)
    {
        if (!string.IsNullOrWhiteSpace(region))
        {
            var regionToken = region.Trim();
            if (_regionsByCode.ContainsKey(regionToken))
            {
                return _regionsByCode[regionToken].Code;
            }

            var fromRegionOnly = _resolver.Resolve(regionToken);
            if (fromRegionOnly.IsKnown && !string.IsNullOrWhiteSpace(fromRegionOnly.RegionCode))
            {
                return fromRegionOnly.RegionCode;
            }
        }

        var resolved = _resolver.ResolveLocation(city, region);
        if (resolved.IsKnown && !string.IsNullOrWhiteSpace(resolved.RegionCode))
        {
            return resolved.RegionCode;
        }

        return UnmappedRegionCode;
    }

    public string GetRegionLabel(string regionCode)
    {
        if (regionCode == GeographyCoverageConstants.AllRegionsToken)
        {
            return "كل المناطق";
        }

        if (string.Equals(regionCode, UnmappedRegionCode, StringComparison.OrdinalIgnoreCase))
        {
            return "غير مصنّف";
        }

        return _regionsByCode.TryGetValue(regionCode, out var region)
            ? region.NameAr
            : regionCode;
    }

    public bool MatchesRegion(string entityRegionCode, string normalizedFilterRegion) =>
        normalizedFilterRegion == GeographyCoverageConstants.AllRegionsToken
        || string.Equals(entityRegionCode, normalizedFilterRegion, StringComparison.OrdinalIgnoreCase);

    private static string MapLegacyRegionCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return GeographyCoverageConstants.AllRegionsToken;
        }

        var token = value.Trim().ToLowerInvariant();
        return token switch
        {
            "all" => GeographyCoverageConstants.AllRegionsToken,
            "central" => "RIYADH",
            "western" => "MAKKAH",
            "eastern" => "EASTERN",
            "northern" => "NORTHERN_BORDERS",
            "southern" => "ASIR",
            "other" => UnmappedRegionCode,
            _ => value.Trim().ToUpperInvariant()
        };
    }
}

public sealed record SaudiRegionRow(string Code, string NameAr, string NameEn, int SortOrder);
