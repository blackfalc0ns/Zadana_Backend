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

    Task<DriverAssignmentDetailDto?> GetAssignmentDetailAsync(
        Guid driverId,
        Guid assignmentId,
        CancellationToken cancellationToken = default);

    Task<DriverCompletedOrdersListDto> GetCompletedOrdersAsync(
        Guid driverId,
        string? status = null,
        CancellationToken cancellationToken = default);

    Task<DriverCompletedOrderDetailDto?> GetCompletedOrderDetailAsync(
        Guid driverId,
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<DriverProfileDto?> GetDriverProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<DeliveryZoneDto[]> GetActiveZonesAsync(CancellationToken cancellationToken = default);

    Task<DeliveryZoneDto[]> GetAllZonesAsync(CancellationToken cancellationToken = default);
}
