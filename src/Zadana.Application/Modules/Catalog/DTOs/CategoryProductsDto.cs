using System.Text.Json.Serialization;

namespace Zadana.Application.Modules.Catalog.DTOs;

public record CategoryProductsAppliedFiltersDto(
    [property: JsonPropertyName("category_id")] Guid? CategoryId,
    [property: JsonPropertyName("subcategory_id")] Guid? SubcategoryId,
    [property: JsonPropertyName("product_type_id")] Guid? ProductTypeId,
    [property: JsonPropertyName("part_id")] Guid? PartId,
    [property: JsonPropertyName("quantity_id")] Guid? QuantityId,
    [property: JsonPropertyName("measurement_value")] decimal? MeasurementValue,
    [property: JsonPropertyName("package_type_id")] Guid? PackageTypeId,
    [property: JsonPropertyName("brand_id")] Guid? BrandId,
    [property: JsonPropertyName("min_price")] decimal? MinPrice,
    [property: JsonPropertyName("max_price")] decimal? MaxPrice,
    [property: JsonPropertyName("sort")] string? Sort);

public record CategoryProductItemDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("store")] string Store,
    [property: JsonPropertyName("price")] decimal Price,
    [property: JsonPropertyName("old_price")] decimal? OldPrice,
    [property: JsonPropertyName("image_url")] string? ImageUrl,
    [property: JsonPropertyName("rating")] decimal? Rating,
    [property: JsonPropertyName("review_count")] int? ReviewCount,
    [property: JsonPropertyName("discount")] string? Discount,
    [property: JsonPropertyName("is_favorite")] bool IsFavorite,
    [property: JsonPropertyName("unit")] string? Unit,
    [property: JsonPropertyName("is_discounted")] bool IsDiscounted,
    [property: JsonPropertyName("show_price_on_card")] bool ShowPriceOnCard = true);

public record CategoryProductsCategoryInfoDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name);

public record CategoryProductsBreadcrumbDto(
    [property: JsonPropertyName("category")] CategoryProductsCategoryInfoDto? Category,
    [property: JsonPropertyName("subcategory")] CategoryProductsCategoryInfoDto? Subcategory);

public record CategoryProductsDto(
    [property: JsonPropertyName("breadcrumb")] CategoryProductsBreadcrumbDto? Breadcrumb,
    [property: JsonPropertyName("applied_filters")] CategoryProductsAppliedFiltersDto AppliedFilters,
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("per_page")] int PerPage,
    [property: JsonPropertyName("items")] IReadOnlyList<CategoryProductItemDto> Items);
