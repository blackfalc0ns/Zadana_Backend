using Zadana.Application.Common.Models;
using Zadana.Application.Modules.Catalog.Queries.ProductRequests.GetPendingRequests;
using Zadana.Application.Modules.Catalog.Queries.ProductRequests.GetVendorRequests;
using Zadana.Domain.Modules.Catalog.Enums;

namespace Zadana.Application.Modules.Catalog.Interfaces;

public interface IProductRequestReadService
{
    Task<PaginatedList<AdminProductRequestDto>> GetPendingAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<PaginatedList<ProductRequestDto>> GetVendorRequestsAsync(
        Guid vendorId,
        ApprovalStatus? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}
