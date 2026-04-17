using Zadana.Domain.Modules.Catalog.Enums;

namespace Zadana.Api.Modules.Catalog.Requests;

public record SubmitProductRequestProductPayload(
    string NameAr,
    string NameEn,
    string? DescriptionAr = null,
    string? DescriptionEn = null,
    Guid? CategoryId = null,
    Guid? BrandId = null,
    Guid? UnitId = null,
    string? ImageUrl = null);

public record SubmitBrandRequestPayload(
    Guid CategoryId,
    string NameAr,
    string NameEn,
    string? LogoUrl = null);

public record SubmitCategoryRequestPayload(
    string NameAr,
    string NameEn,
    string TargetLevel,
    Guid? ParentCategoryId = null,
    int DisplayOrder = 1,
    string? ImageUrl = null);

public record SubmitProductRequestRequest(
    SubmitProductRequestProductPayload Product,
    SubmitBrandRequestPayload? RequestedBrand = null,
    SubmitCategoryRequestPayload? RequestedCategory = null);

public record GetVendorProductRequestsRequest(
    int PageNumber = 1,
    int PageSize = 10,
    ApprovalStatus? Status = null);

public record GetPendingProductRequestsRequest(
    int PageNumber = 1,
    int PageSize = 10);

public record ReviewProductRequestRequest(
    bool IsApproved,
    string? RejectionReason);

public record ReviewCategoryRequestRequest(
    bool IsApproved,
    string? RejectionReason,
    string? ApprovedTargetLevel = null,
    Guid? ApprovedParentCategoryId = null);

public record GetCatalogRequestCenterRequest(
    string? Type = null,
    string? Status = null,
    Guid? VendorId = null,
    int PageNumber = 1,
    int PageSize = 20);
