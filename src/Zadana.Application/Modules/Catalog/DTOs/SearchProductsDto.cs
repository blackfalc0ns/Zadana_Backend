using System.Text.Json.Serialization;

namespace Zadana.Application.Modules.Catalog.DTOs;

public record SearchProductItemDto(
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
    [property: JsonPropertyName("name_ar")] string? NameAr = null,
    [property: JsonPropertyName("name_en")] string? NameEn = null,
    [property: JsonPropertyName("store_ar")] string? StoreAr = null,
    [property: JsonPropertyName("store_en")] string? StoreEn = null,
    [property: JsonPropertyName("unit_ar")] string? UnitAr = null,
    [property: JsonPropertyName("unit_en")] string? UnitEn = null);

public record SearchProductsResponseDto(
    [property: JsonPropertyName("query")] string Query,
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("per_page")] int PerPage,
    [property: JsonPropertyName("items")] IReadOnlyList<SearchProductItemDto> Items);
