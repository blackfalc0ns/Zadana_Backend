namespace Zadana.Application.Modules.Geography;

public sealed record ResolvedCity(
    string? CityCode,
    string? RegionCode,
    string? CityNameAr,
    string? CityNameEn,
    GeographyCityMatchQuality MatchQuality)
{
    public bool IsKnown => !string.IsNullOrWhiteSpace(CityCode)
        && !string.Equals(CityCode, GeographyCoverageConstants.UnmappedCityCode, StringComparison.Ordinal);

    public static ResolvedCity Unknown { get; } = new(
        GeographyCoverageConstants.UnmappedCityCode,
        null,
        null,
        null,
        GeographyCityMatchQuality.Unknown);
}
