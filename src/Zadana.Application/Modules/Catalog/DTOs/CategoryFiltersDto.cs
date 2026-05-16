using System.Text.Json.Serialization;

namespace Zadana.Application.Modules.Catalog.DTOs;

public record CatalogFilterNamedItemDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name);

public record CatalogMeasurementOptionDto(
    [property: JsonPropertyName("measurement_value")] decimal MeasurementValue,
    [property: JsonPropertyName("measurement_unit_id")] Guid MeasurementUnitId,
    [property: JsonPropertyName("measurement_unit_name")] string MeasurementUnitName,
    [property: JsonPropertyName("label")] string Label);

public record CategoryFilterCategoryItemDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("image_url")] string? ImageUrl);

public record CatalogFilterPartItemDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("product_type_id")] Guid? ProductTypeId);

public record CatalogFilterBrandItemDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("logo_url")] string? LogoUrl);

public record CatalogFilterPriceRangeDto(
    [property: JsonPropertyName("min")] decimal Min,
    [property: JsonPropertyName("max")] decimal Max);

public record CatalogSortOptionDto(
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("value")] string Value);

public record CategoryFiltersDto(
    [property: JsonPropertyName("category")] CategoryFilterCategoryItemDto Category,
    [property: JsonPropertyName("subcategories")] IReadOnlyList<CategoryFilterCategoryItemDto> Subcategories,
    [property: JsonPropertyName("product_types")] IReadOnlyList<CatalogFilterNamedItemDto> ProductTypes,
    [property: JsonPropertyName("parts")] IReadOnlyList<CatalogFilterPartItemDto> Parts,
    [property: JsonPropertyName("quantities")] IReadOnlyList<CatalogFilterNamedItemDto> Quantities,
    [property: JsonPropertyName("package_types")] IReadOnlyList<CatalogFilterNamedItemDto> PackageTypes,
    [property: JsonPropertyName("measurement_values")] IReadOnlyList<decimal> MeasurementValues,
    [property: JsonPropertyName("measurement_options")] IReadOnlyList<CatalogMeasurementOptionDto> MeasurementOptions,
    [property: JsonPropertyName("brands")] IReadOnlyList<CatalogFilterBrandItemDto> Brands,
    [property: JsonPropertyName("price_range")] CatalogFilterPriceRangeDto PriceRange,
    [property: JsonPropertyName("sort_options")] IReadOnlyList<CatalogSortOptionDto> SortOptions);
