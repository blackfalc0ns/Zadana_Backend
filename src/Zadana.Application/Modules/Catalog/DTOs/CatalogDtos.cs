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
    Guid? PackageTypeId,
    string? PackageTypeNameAr,
    string? PackageTypeNameEn,
    decimal? MeasurementValue,
    Guid? MeasurementUnitId,
    string? MeasurementUnitNameAr,
    string? MeasurementUnitNameEn,
    Guid VariantGroupId,
    string? DisplaySizeAr,
    string? DisplaySizeEn,
    string Status,
    bool ShowPriceOnCard,
    bool IsInVendorStore,
    ICollection<MasterProductImageDto> Images,
    DateTime? CreatedAtUtc = null,
    DateTime? UpdatedAtUtc = null,
    ICollection<MasterProductVariantOptionDto>? Variants = null,
    decimal? VendorSellingPrice = null,
    decimal? VendorCompareAtPrice = null,
    decimal? VendorCostPrice = null,
    decimal? VendorTradePrice = null);

public record VendorProductDto(
    Guid Id,
    Guid VendorId,
    Guid MasterProductId,
    decimal? CostPrice,
    decimal? TradePrice,
    decimal SellingPrice,
    decimal? CompareAtPrice,
    decimal? CommissionRate,
    int StockQuantity,
    bool IsAvailable,
    string Status,
    MasterProductDto MasterProduct,
    Guid? VendorBranchId = null);

public record ProductVendorSnapshotDto(
    Guid VendorId,
    string NameAr,
    string NameEn,
    int Quantity,
    decimal Price,
    DateTime UpdatedAtUtc);
