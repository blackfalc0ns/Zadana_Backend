using Zadana.Application.Modules.Catalog.Commands.CreateMasterProduct;
using Zadana.Domain.Modules.Catalog.Enums;

namespace Zadana.Api.Modules.Catalog.Requests;

public record CreateBrandRequest(string NameAr, string NameEn, string? LogoUrl, Guid CategoryId);

public record UpdateBrandRequest(
    string NameAr,
    string NameEn,
    string? LogoUrl,
    Guid CategoryId,
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
    ProductStatus Status = ProductStatus.Draft,
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
    ProductStatus? Status = null,
    List<CreateProductImageInfo>? Images = null);

public record CatalogPaginationRequest(
    int PageNumber = 1,
    int PageSize = 10);

public record CatalogSortRequest(
    string? Field = null,
    string? Direction = null);

public record ProductSearchFiltersRequest(
    List<Guid>? SubcategoryIds = null,
    List<Guid>? BrandIds = null,
    List<ProductStatus>? Statuses = null,
    bool? IsActiveBrand = null,
    bool? HasBrand = null);

public record ProductSearchRequest(
    CatalogPaginationRequest? Pagination = null,
    CatalogSortRequest? Sort = null,
    string? Search = null,
    ProductSearchFiltersRequest? Filters = null);

public record CategorySearchFiltersRequest(
    Guid? ParentCategoryId = null,
    int? Level = null,
    bool? IsActive = null,
    bool? HasChildren = null,
    DateTime? CreatedAtFrom = null,
    DateTime? CreatedAtTo = null);

public record CategorySearchRequest(
    CatalogPaginationRequest? Pagination = null,
    CatalogSortRequest? Sort = null,
    string? Search = null,
    CategorySearchFiltersRequest? Filters = null);

public record BrandSearchFiltersRequest(
    Guid? CategoryId = null,
    bool? IsActive = null,
    bool? HasProducts = null,
    DateTime? CreatedAtFrom = null,
    DateTime? CreatedAtTo = null);

public record BrandSearchRequest(
    CatalogPaginationRequest? Pagination = null,
    CatalogSortRequest? Sort = null,
    string? Search = null,
    BrandSearchFiltersRequest? Filters = null);
