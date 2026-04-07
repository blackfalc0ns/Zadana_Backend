using Zadana.Application.Common.Models;
using Zadana.Application.Modules.Catalog.DTOs;

namespace Zadana.Application.Modules.Catalog.Interfaces;

public interface ICatalogRequestReadService
{
    Task<PaginatedList<CatalogRequestListItemDto>> GetAdminRequestsAsync(
        string? requestType,
        string? status,
        Guid? vendorId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<CatalogRequestDetailDto?> GetAdminRequestDetailAsync(
        string requestType,
        Guid requestId,
        CancellationToken cancellationToken = default);

    Task<PaginatedList<CatalogRequestListItemDto>> GetVendorRequestsAsync(
        Guid vendorId,
        string? requestType,
        string? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VendorCatalogNotificationDto>> GetVendorNotificationsAsync(
        Guid vendorUserId,
        CancellationToken cancellationToken = default);
}
