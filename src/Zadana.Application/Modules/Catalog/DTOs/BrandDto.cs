namespace Zadana.Application.Modules.Catalog.DTOs;

public record BrandDto(
    Guid Id,
    string NameAr,
    string NameEn,
    string? LogoUrl,
    bool IsActive);
