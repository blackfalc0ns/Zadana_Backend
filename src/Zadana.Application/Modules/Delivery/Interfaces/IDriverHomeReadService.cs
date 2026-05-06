using Zadana.Application.Modules.Delivery.DTOs;

namespace Zadana.Application.Modules.Delivery.Interfaces;

public interface IDriverHomeReadService
{
    Task<DriverHomeDto> GetHomeAsync(
        Guid driverUserId,
        bool processExpiredOffers = false,
        CancellationToken cancellationToken = default);
}
