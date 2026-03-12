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
    Guid? UnitOfMeasureId,
    string Status,
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
