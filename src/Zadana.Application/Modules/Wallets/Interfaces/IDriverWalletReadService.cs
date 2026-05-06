using Zadana.Application.Modules.Wallets.DTOs;

namespace Zadana.Application.Modules.Wallets.Interfaces;

public interface IDriverWalletReadService
{
    Task<DriverWalletSummaryDto> GetWalletSummaryAsync(
        Guid driverUserId,
        CancellationToken cancellationToken = default);

    Task<DriverWalletRealtimePayload> GetRealtimePayloadAsync(
        Guid driverUserId,
        CancellationToken cancellationToken = default);
}
