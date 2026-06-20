using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Geography;

namespace Zadana.Application.Modules.Identity.Support;

public static class CustomerCityLocalization
{
    public static CustomerCityLabels Localize(IGeographyCityResolver resolver, string? rawCity)
    {
        if (string.IsNullOrWhiteSpace(rawCity))
        {
            return CustomerCityLabels.Empty;
        }

        var trimmed = rawCity.Trim();
        var resolved = resolver.Resolve(trimmed);
        if (resolved.IsKnown)
        {
            return new CustomerCityLabels(
                resolved.CityCode,
                resolved.CityNameAr ?? trimmed,
                resolved.CityNameEn ?? trimmed,
                trimmed);
        }

        return new CustomerCityLabels(trimmed, trimmed, trimmed, trimmed);
    }

    public static bool MatchesFilter(IGeographyCityResolver resolver, string? addressCity, string filterValue)
    {
        if (string.IsNullOrWhiteSpace(addressCity) || string.IsNullOrWhiteSpace(filterValue))
        {
            return false;
        }

        var filterResolved = resolver.Resolve(filterValue.Trim());
        var addressResolved = resolver.Resolve(addressCity.Trim());

        if (filterResolved.IsKnown && addressResolved.IsKnown)
        {
            return string.Equals(filterResolved.CityCode, addressResolved.CityCode, StringComparison.OrdinalIgnoreCase);
        }

        if (filterResolved.IsKnown)
        {
            return addressResolved.IsKnown
                ? string.Equals(filterResolved.CityCode, addressResolved.CityCode, StringComparison.OrdinalIgnoreCase)
                : string.Equals(addressCity.Trim(), filterValue.Trim(), StringComparison.OrdinalIgnoreCase)
                    || string.Equals(addressCity.Trim(), filterResolved.CityNameAr, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(addressCity.Trim(), filterResolved.CityNameEn, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(addressCity.Trim(), filterValue.Trim(), StringComparison.OrdinalIgnoreCase)
            || addressCity.Contains(filterValue.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record CustomerCityLabels(
    string? Code,
    string? Ar,
    string? En,
    string? Raw)
{
    public static CustomerCityLabels Empty { get; } = new(null, null, null, null);
}
