using Zadana.Application.Modules.Orders.DTOs;

namespace Zadana.Application.Modules.Orders.Interfaces;

public interface IOrderReadService
{
    Task<OrderDto?> GetByIdAsync(Guid orderId, Guid userId, CancellationToken cancellationToken = default);
}
