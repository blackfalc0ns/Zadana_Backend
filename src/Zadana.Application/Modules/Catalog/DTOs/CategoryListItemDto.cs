using System.Text.Json.Serialization;

namespace Zadana.Application.Modules.Catalog.DTOs;

public record CategoryListItemDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("image_url")] string? ImageUrl,
    [property: JsonPropertyName("name_ar")] string? NameAr = null,
    [property: JsonPropertyName("name_en")] string? NameEn = null);
