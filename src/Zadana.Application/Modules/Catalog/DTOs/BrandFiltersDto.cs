using System.Text.Json.Serialization;

namespace Zadana.Application.Modules.Catalog.DTOs;

public record BrandFilterSubcategoryItemDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("category_id")] Guid CategoryId);

public record BrandFiltersDto(
    [property: JsonPropertyName("brand")] CatalogFilterNamedItemDto Brand,
    [property: JsonPropertyName("categories")] IReadOnlyList<CatalogFilterNamedItemDto> Categories,
    [property: JsonPropertyName("subcategories")] IReadOnlyList<BrandFilterSubcategoryItemDto> Subcategories,
    [property: JsonPropertyName("units")] IReadOnlyList<CatalogFilterNamedItemDto> Units,
    [property: JsonPropertyName("price_range")] CatalogFilterPriceRangeDto PriceRange,
    [property: JsonPropertyName("sort_options")] IReadOnlyList<CatalogSortOptionDto> SortOptions);
