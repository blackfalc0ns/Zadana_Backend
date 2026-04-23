using Zadana.Application.Modules.Delivery.DTOs;

namespace Zadana.Application.Modules.Delivery.Interfaces;

public interface IDeliveryDispatchService
{
    /// <summary>
    /// Attempts to auto-dispatch a driver for the given order.
    /// Returns the dispatch decision if a driver was found, null otherwise.
    /// </summary>
    Task<DispatchDecisionDto?> TryAutoDispatchAsync(Guid orderId, CancellationToken cancellationToken = default);
}
