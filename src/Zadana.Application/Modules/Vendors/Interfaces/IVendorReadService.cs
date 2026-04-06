using Zadana.Application.Common.Models;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Domain.Modules.Vendors.Enums;

namespace Zadana.Application.Modules.Vendors.Interfaces;

public interface IVendorReadService
{
    Task<PaginatedList<VendorListItemDto>> GetAllAsync(
        VendorStatus? status,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<VendorDetailDto?> GetDetailAsync(Guid vendorId, CancellationToken cancellationToken = default);

    Task<VendorWorkspaceDto?> GetWorkspaceByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<Guid?> GetVendorIdByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
