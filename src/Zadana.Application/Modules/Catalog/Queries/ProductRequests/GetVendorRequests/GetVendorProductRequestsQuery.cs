using MediatR;
using Zadana.Application.Common.Models;
using Zadana.Domain.Modules.Catalog.Enums;

namespace Zadana.Application.Modules.Catalog.Queries.ProductRequests.GetVendorRequests;

public record GetVendorProductRequestsQuery(
    int PageNumber = 1,
    int PageSize = 10,
    ApprovalStatus? Status = null
) : IRequest<PaginatedList<ProductRequestDto>>;

public record ProductRequestDto(
    Guid Id,
    string SuggestedNameAr,
    string SuggestedNameEn,
    string? SuggestedDescriptionAr,
    string? SuggestedDescriptionEn,
    Guid SuggestedCategoryId,
    string CategoryNameAr,
    string CategoryNameEn,
    string? ImageUrl,
    string Status,
    string? RejectionReason,
    DateTime CreatedAt
);
