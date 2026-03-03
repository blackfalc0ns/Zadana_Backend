namespace Zadana.Application.Modules.Catalog.DTOs;

public record CategoryDto(
    Guid Id,
    string NameAr,
    string NameEn,
    Guid? ParentCategoryId,
    int DisplayOrder,
    bool IsActive);

public record BrandDto(
    Guid Id,
    string Name,
    string? LogoUrl,
    bool IsActive);

public record UnitOfMeasureDto(
    Guid Id,
    string NameAr,
    string NameEn,
    bool IsActive);

public record MasterProductDto(
    Guid Id,
    string NameAr,
    string NameEn,
    string? DescriptionAr,
    string? DescriptionEn,
    string? Barcode,
    Guid CategoryId,
    Guid? BrandId,
    Guid? UnitOfMeasureId,
    string Status);

public record VendorProductDto(
    Guid Id,
    Guid VendorId,
    Guid MasterProductId,
    decimal SellingPrice,
    decimal? CompareAtPrice,
    int StockQuantity,
    bool IsAvailable,
    string Status,
    MasterProductDto MasterProduct);
