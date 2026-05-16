using System.Text.Json.Serialization;

namespace Zadana.Application.Modules.Catalog.DTOs;

public record ProductDetailsVendorPriceDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("logo_url")] string? LogoUrl,
    [property: JsonPropertyName("price")] decimal Price,
    [property: JsonPropertyName("old_price")] decimal? OldPrice,
    [property: JsonPropertyName("is_discounted")] bool IsDiscounted);

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
    [property: JsonPropertyName("is_discounted")] bool IsDiscounted);

public record ProductDetailsVariantOptionDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("default_vendor_product_id")] Guid? DefaultVendorProductId,
    [property: JsonPropertyName("name_ar")] string NameAr,
    [property: JsonPropertyName("name_en")] string NameEn,
    [property: JsonPropertyName("display_size_ar")] string? DisplaySizeAr,
    [property: JsonPropertyName("display_size_en")] string? DisplaySizeEn,
    [property: JsonPropertyName("is_current")] bool IsCurrent,
    [property: JsonPropertyName("image_url")] string? ImageUrl,
    [property: JsonPropertyName("images")] IReadOnlyList<string> Images,
    [property: JsonPropertyName("package_type_name_ar")] string? PackageTypeNameAr,
    [property: JsonPropertyName("package_type_name_en")] string? PackageTypeNameEn,
    [property: JsonPropertyName("measurement_value")] decimal? MeasurementValue,
    [property: JsonPropertyName("measurement_unit_name_ar")] string? MeasurementUnitNameAr,
    [property: JsonPropertyName("measurement_unit_name_en")] string? MeasurementUnitNameEn,
    [property: JsonPropertyName("unit")] string? Unit,
    [property: JsonPropertyName("price")] decimal? Price,
    [property: JsonPropertyName("old_price")] decimal? OldPrice,
    [property: JsonPropertyName("is_discounted")] bool IsDiscounted,
    [property: JsonPropertyName("vendor_prices")] IReadOnlyList<ProductDetailsVendorPriceDto> VendorPrices);

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
    [property: JsonPropertyName("variant_options")] IReadOnlyList<ProductDetailsVariantOptionDto> VariantOptions,
    [property: JsonPropertyName("vendor_prices")] IReadOnlyList<ProductDetailsVendorPriceDto> VendorPrices,
    [property: JsonPropertyName("similar_products")] IReadOnlyList<ProductDetailsSimilarProductDto> SimilarProducts);
