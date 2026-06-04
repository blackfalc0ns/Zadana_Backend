namespace Zadana.Application.Modules.Geography;

public static class GeographyRegionFilter
{
    public static string Normalize(string? region)
    {
        if (string.IsNullOrWhiteSpace(region))
        {
            return GeographyCoverageConstants.AllRegionsToken;
        }

        var trimmed = region.Trim();
        if (trimmed.Equals(GeographyCoverageConstants.AllRegionsToken, StringComparison.OrdinalIgnoreCase))
        {
            return GeographyCoverageConstants.AllRegionsToken;
        }

        return trimmed.ToUpperInvariant();
    }

    public static bool MatchesCity(AdminGeographyCoverageCityFilter city, string normalizedRegion)
    {
        if (normalizedRegion == GeographyCoverageConstants.AllRegionsToken)
        {
            return true;
        }

        if (city.CityCode == GeographyCoverageConstants.UnmappedCityCode)
        {
            return false;
        }

        return string.Equals(city.RegionCode, normalizedRegion, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record AdminGeographyCoverageCityFilter(string CityCode, string RegionCode);
