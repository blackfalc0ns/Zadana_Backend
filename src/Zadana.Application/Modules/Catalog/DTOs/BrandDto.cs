namespace Zadana.Application.Modules.Catalog.DTOs;

public record BrandDto(
    Guid Id,
    string NameAr,
    string NameEn,
    string? LogoUrl,
    Guid? CategoryId,
    string? CategoryNameAr,
    string? CategoryNameEn,
    bool IsActive,
    int MasterProductsCount,
    DateTime? CreatedAtUtc,
    DateTime? UpdatedAtUtc);

