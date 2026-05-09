using System.Text.Json.Serialization;

namespace Zadana.Application.Modules.Catalog.DTOs;

public record BrandCustomerDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("logo")] string? Logo,
    [property: JsonPropertyName("cover_image_url")] string? CoverImageUrl,
    [property: JsonPropertyName("product_count")] int ProductCount,
    [property: JsonPropertyName("name_ar")] string? NameAr = null,
    [property: JsonPropertyName("name_en")] string? NameEn = null);
