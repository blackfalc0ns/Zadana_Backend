namespace Zadana.Application.Modules.Catalog.DTOs;



public record MasterProductImageDto(
    string Url,
    string? AltText,
    int DisplayOrder,
    bool IsPrimary);

public record MasterProductDto(
    Guid Id,
    string NameAr,
    string NameEn,
    string Slug,
    string? DescriptionAr,
    string? DescriptionEn,
    string? Barcode,
    Guid CategoryId,
    Guid? BrandId,
    string? BrandNameAr,
    string? BrandNameEn,
    Guid? UnitOfMeasureId,
    string? UnitNameAr,
    string? UnitNameEn,
    string Status,
    bool IsInVendorStore,
    ICollection<MasterProductImageDto> Images);

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

public record ProductVendorSnapshotDto(
    Guid VendorId,
    string NameAr,
    string NameEn,
    int Quantity,
    decimal Price,
    DateTime UpdatedAtUtc);
