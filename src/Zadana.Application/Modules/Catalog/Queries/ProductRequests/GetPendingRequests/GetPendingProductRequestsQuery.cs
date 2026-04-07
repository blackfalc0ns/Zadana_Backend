using MediatR;
using Zadana.Application.Common.Models;
using Zadana.Application.Modules.Catalog.Queries.ProductRequests.GetVendorRequests;

namespace Zadana.Application.Modules.Catalog.Queries.ProductRequests.GetPendingRequests;

public record GetPendingProductRequestsQuery(
    int PageNumber = 1,
    int PageSize = 10
) : IRequest<PaginatedList<AdminProductRequestDto>>;

public record AdminProductRequestDto(
    Guid Id,
    Guid VendorId,
    string VendorName,
    string SuggestedNameAr,
    string SuggestedNameEn,
    string? SuggestedDescriptionAr,
    string? SuggestedDescriptionEn,
    Guid? SuggestedCategoryId,
    string? CategoryNameAr,
    string? CategoryNameEn,
    string? ImageUrl,
    DateTime CreatedAt
);
