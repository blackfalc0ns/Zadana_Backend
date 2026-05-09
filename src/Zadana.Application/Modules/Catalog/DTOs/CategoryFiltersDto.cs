using System.Text.Json.Serialization;

namespace Zadana.Application.Modules.Catalog.DTOs;

public record CatalogFilterNamedItemDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name);

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
    [property: JsonPropertyName("category")] CatalogFilterNamedItemDto Category,
    [property: JsonPropertyName("subcategories")] IReadOnlyList<CatalogFilterNamedItemDto> Subcategories,
    [property: JsonPropertyName("product_types")] IReadOnlyList<CatalogFilterNamedItemDto> ProductTypes,
    [property: JsonPropertyName("parts")] IReadOnlyList<CatalogFilterPartItemDto> Parts,
    [property: JsonPropertyName("quantities")] IReadOnlyList<CatalogFilterNamedItemDto> Quantities,
    [property: JsonPropertyName("brands")] IReadOnlyList<CatalogFilterBrandItemDto> Brands,
    [property: JsonPropertyName("price_range")] CatalogFilterPriceRangeDto PriceRange,
    [property: JsonPropertyName("sort_options")] IReadOnlyList<CatalogSortOptionDto> SortOptions);
