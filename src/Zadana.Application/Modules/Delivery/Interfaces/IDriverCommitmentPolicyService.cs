using Zadana.Application.Modules.Delivery.DTOs;

namespace Zadana.Application.Modules.Delivery.Interfaces;

public interface IDriverCommitmentPolicyService
{
    Task<DriverCommitmentSummaryDto> GetDriverSummaryAsync(
        Guid driverId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, DriverCommitmentSummaryDto>> GetDriverSummariesAsync(
        IReadOnlyCollection<Guid> driverIds,
        CancellationToken cancellationToken = default);

    Task ApplyOperationalEnforcementAsync(
        IReadOnlyCollection<Guid> driverIds,
        CancellationToken cancellationToken = default);
}
