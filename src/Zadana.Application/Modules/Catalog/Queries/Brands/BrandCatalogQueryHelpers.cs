using System.Globalization;
using Zadana.Application.Modules.Catalog.DTOs;

namespace Zadana.Application.Modules.Catalog.Queries.Brands;

internal static class BrandCatalogQueryHelpers
{
    public static string PickLocalized(string? arabic, string? english)
    {
        var preferred = IsArabic() ? arabic : english;
        var fallback = IsArabic() ? english : arabic;
        return preferred?.Trim()
            ?? fallback?.Trim()
            ?? string.Empty;
    }

    public static string? PickLocalizedNullable(string? arabic, string? english)
    {
        var value = PickLocalized(arabic, english);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public static string? NormalizeSort(string? sort)
    {
        var normalized = sort?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "newest" => "newest",
            "price_low_high" => "price_low_high",
            "price_high_low" => "price_high_low",
            "best_selling" => "best_selling",
            "highest_rated" => "highest_rated",
            "alphabetical" => "alphabetical",
            _ => null
        };
    }

    public static IReadOnlyList<CatalogSortOptionDto> BuildSortOptions() =>
        IsArabic()
            ? new[]
            {
                new CatalogSortOptionDto("\u0627\u0644\u0623\u062D\u062F\u062B", "newest"),
                new CatalogSortOptionDto("\u0627\u0644\u0633\u0639\u0631: \u0645\u0646 \u0627\u0644\u0623\u0642\u0644 \u0625\u0644\u0649 \u0627\u0644\u0623\u0639\u0644\u0649", "price_low_high"),
                new CatalogSortOptionDto("\u0627\u0644\u0633\u0639\u0631: \u0645\u0646 \u0627\u0644\u0623\u0639\u0644\u0649 \u0625\u0644\u0649 \u0627\u0644\u0623\u0642\u0644", "price_high_low"),
                new CatalogSortOptionDto("\u0627\u0644\u0623\u0643\u062B\u0631 \u0645\u0628\u064A\u0639\u064B\u0627", "best_selling"),
                new CatalogSortOptionDto("\u0627\u0644\u0623\u0639\u0644\u0649 \u062A\u0642\u064A\u064A\u0645\u064B\u0627", "highest_rated"),
                new CatalogSortOptionDto("\u0623\u0628\u062C\u062F\u064A\u064B\u0627", "alphabetical")
            }
            : new[]
            {
                new CatalogSortOptionDto("Newest", "newest"),
                new CatalogSortOptionDto("Price: Low to High", "price_low_high"),
                new CatalogSortOptionDto("Price: High to Low", "price_high_low"),
                new CatalogSortOptionDto("Best Selling", "best_selling"),
                new CatalogSortOptionDto("Highest Rated", "highest_rated"),
                new CatalogSortOptionDto("Alphabetical", "alphabetical")
            };

    private static bool IsArabic() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase);
}
