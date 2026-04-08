using System.Text.Json.Serialization;

namespace Zadana.Application.Modules.Catalog.DTOs;

public record BrandCustomerDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("logo")] string? Logo,
    [property: JsonPropertyName("product_count")] int ProductCount);
