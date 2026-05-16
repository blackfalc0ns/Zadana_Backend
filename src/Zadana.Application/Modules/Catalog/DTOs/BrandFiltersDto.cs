using System.Text.Json.Serialization;

namespace Zadana.Application.Modules.Catalog.DTOs;

public record BrandFilterCategoryItemDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("image_url")] string? ImageUrl);

public record BrandFilterSubcategoryItemDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("category_id")] Guid CategoryId,
    [property: JsonPropertyName("image_url")] string? ImageUrl);

public record BrandFilterBrandItemDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("logo_url")] string? LogoUrl);

public record BrandFiltersDto(
    [property: JsonPropertyName("brand")] BrandFilterBrandItemDto Brand,
    [property: JsonPropertyName("categories")] IReadOnlyList<BrandFilterCategoryItemDto> Categories,
    [property: JsonPropertyName("subcategories")] IReadOnlyList<BrandFilterSubcategoryItemDto> Subcategories,
    [property: JsonPropertyName("units")] IReadOnlyList<CatalogFilterNamedItemDto> Units,
    [property: JsonPropertyName("package_types")] IReadOnlyList<CatalogFilterNamedItemDto> PackageTypes,
    [property: JsonPropertyName("measurement_values")] IReadOnlyList<decimal> MeasurementValues,
    [property: JsonPropertyName("price_range")] CatalogFilterPriceRangeDto PriceRange,
    [property: JsonPropertyName("sort_options")] IReadOnlyList<CatalogSortOptionDto> SortOptions);
