using System.Text.Json.Serialization;

namespace Zadana.Application.Modules.Catalog.DTOs;

public record ProductDetailsVendorPriceDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("logo_url")] string? LogoUrl,
    [property: JsonPropertyName("price")] decimal Price,
    [property: JsonPropertyName("old_price")] decimal? OldPrice,
    [property: JsonPropertyName("is_discounted")] bool IsDiscounted,
    [property: JsonPropertyName("name_ar")] string? NameAr = null,
    [property: JsonPropertyName("name_en")] string? NameEn = null);

public record ProductDetailsSimilarProductDto(
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

public record ProductDetailsDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("master_product_id")] Guid MasterProductId,
    [property: JsonPropertyName("default_vendor_product_id")] Guid DefaultVendorProductId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("store")] string Store,
    [property: JsonPropertyName("price")] decimal Price,
    [property: JsonPropertyName("old_price")] decimal? OldPrice,
    [property: JsonPropertyName("image_url")] string? ImageUrl,
    [property: JsonPropertyName("images")] IReadOnlyList<string> Images,
    [property: JsonPropertyName("rating")] decimal? Rating,
    [property: JsonPropertyName("review_count")] int? ReviewCount,
    [property: JsonPropertyName("discount")] string? Discount,
    [property: JsonPropertyName("is_favorite")] bool IsFavorite,
    [property: JsonPropertyName("unit")] string? Unit,
    [property: JsonPropertyName("is_discounted")] bool IsDiscounted,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("vendor_prices")] IReadOnlyList<ProductDetailsVendorPriceDto> VendorPrices,
    [property: JsonPropertyName("similar_products")] IReadOnlyList<ProductDetailsSimilarProductDto> SimilarProducts,
    [property: JsonPropertyName("name_ar")] string? NameAr = null,
    [property: JsonPropertyName("name_en")] string? NameEn = null,
    [property: JsonPropertyName("store_ar")] string? StoreAr = null,
    [property: JsonPropertyName("store_en")] string? StoreEn = null,
    [property: JsonPropertyName("unit_ar")] string? UnitAr = null,
    [property: JsonPropertyName("unit_en")] string? UnitEn = null,
    [property: JsonPropertyName("description_ar")] string? DescriptionAr = null,
    [property: JsonPropertyName("description_en")] string? DescriptionEn = null);
