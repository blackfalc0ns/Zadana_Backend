using Zadana.Domain.Modules.Catalog.Enums;

namespace Zadana.Api.Modules.Catalog.Requests;

public record SubmitProductRequestRequest(
    string SuggestedNameAr,
    string SuggestedNameEn,
    Guid SuggestedCategoryId,
    string? SuggestedDescriptionAr = null,
    string? SuggestedDescriptionEn = null,
    string? ImageUrl = null);

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
