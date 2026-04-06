using Zadana.Application.Modules.Catalog.Commands.CreateMasterProduct;

namespace Zadana.Api.Modules.Catalog.Requests;

public record CreateBrandRequest(string NameAr, string NameEn, string? LogoUrl);

public record UpdateBrandRequest(
    string NameAr,
    string NameEn,
    string? LogoUrl,
    bool IsActive);

public record CreateCategoryRequest(
    string NameAr,
    string NameEn,
    string? ImageUrl,
    Guid? ParentCategoryId,
    int DisplayOrder);

public record UpdateCategoryRequest(
    string NameAr,
    string NameEn,
    string? ImageUrl,
    Guid? ParentCategoryId,
    int DisplayOrder,
    bool IsActive);

public record CreateUnitRequest(
    string NameAr,
    string NameEn,
    string? Symbol);

public record UpdateUnitRequest(
    string NameAr,
    string NameEn,
    string? Symbol,
    bool IsActive);

public record CreateMasterProductRequest(
    Guid CategoryId,
    string NameAr,
    string NameEn,
    string Slug,
    string? Barcode,
    string? DescriptionAr,
    string? DescriptionEn,
    Guid? BrandId,
    Guid? UnitId,
    List<CreateProductImageInfo>? Images = null);

public record UpdateMasterProductRequest(
    Guid CategoryId,
    string NameAr,
    string NameEn,
    string Slug,
    string? Barcode,
    string? DescriptionAr,
    string? DescriptionEn,
    Guid? BrandId,
    Guid? UnitId,
    List<CreateProductImageInfo>? Images = null);
