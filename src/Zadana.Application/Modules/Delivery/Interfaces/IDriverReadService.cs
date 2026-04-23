using Zadana.Application.Modules.Delivery.DTOs;

namespace Zadana.Application.Modules.Delivery.Interfaces;

public interface IDriverReadService
{
    Task<AdminDriversListDto> GetAdminDriversAsync(
        string? search,
        string? city,
        string? status,
        string? verificationStatus,
        string? vehicleType,
        string? performance,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<AdminDriverDetailDto?> GetAdminDriverDetailAsync(
        Guid driverId,
        CancellationToken cancellationToken = default);

    Task<DeliveryZoneDto[]> GetActiveZonesAsync(CancellationToken cancellationToken = default);

    Task<DeliveryZoneDto[]> GetAllZonesAsync(CancellationToken cancellationToken = default);
}
