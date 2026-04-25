using Zadana.Application.Modules.Delivery.DTOs;

namespace Zadana.Application.Modules.Delivery.Interfaces;

public interface IDeliveryDispatchService
{
    Task<DispatchDecisionDto?> TryAutoDispatchAsync(Guid orderId, bool resetCycle = false, CancellationToken cancellationToken = default);
    Task ProcessExpiredOffersAsync(CancellationToken cancellationToken = default);
    Task<DriverOfferActionResultDto> AcceptOfferAsync(Guid assignmentId, Guid driverId, CancellationToken cancellationToken = default);
    Task<DriverOfferActionResultDto> RejectOfferAsync(Guid assignmentId, Guid driverId, string? reason, CancellationToken cancellationToken = default);
}
