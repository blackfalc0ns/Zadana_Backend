using Zadana.Application.Common.Models;
using Zadana.Application.Modules.Orders.DTOs;

namespace Zadana.Application.Modules.Orders.Interfaces;

public interface IOrderReadService
{
    Task<OrderDto?> GetByIdAsync(Guid orderId, Guid userId, CancellationToken cancellationToken = default);
    Task<PaginatedList<AdminVendorOrderListItemDto>> GetVendorOrdersAsync(
        Guid vendorId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
