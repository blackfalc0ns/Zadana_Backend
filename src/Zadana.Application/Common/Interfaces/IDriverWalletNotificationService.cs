using Zadana.Domain.Modules.Wallets.Entities;

namespace Zadana.Application.Common.Interfaces;

public interface IDriverWalletNotificationService
{
    Task NotifyWithdrawalSubmittedAsync(
        Guid driverUserId,
        DriverWithdrawalRequest withdrawal,
        CancellationToken cancellationToken = default);

    Task NotifyWithdrawalCancelledAsync(
        Guid driverUserId,
        DriverWithdrawalRequest withdrawal,
        CancellationToken cancellationToken = default);

    Task NotifyWithdrawalProcessingAsync(
        Guid driverUserId,
        DriverWithdrawalRequest withdrawal,
        CancellationToken cancellationToken = default);

    Task NotifyWithdrawalRejectedAsync(
        Guid driverUserId,
        DriverWithdrawalRequest withdrawal,
        CancellationToken cancellationToken = default);

    Task NotifyWithdrawalFailedAsync(
        Guid driverUserId,
        DriverWithdrawalRequest withdrawal,
        CancellationToken cancellationToken = default);

    Task NotifyWithdrawalPaidAsync(
        Guid driverUserId,
        DriverWithdrawalRequest withdrawal,
        CancellationToken cancellationToken = default,
        bool hasTransferProof = false);

    Task NotifyWithdrawalReturnedAsync(
        Guid driverUserId,
        DriverWithdrawalRequest withdrawal,
        Guid payoutId,
        decimal amount,
        string? reason,
        CancellationToken cancellationToken = default);

    Task NotifyAdminWalletAdjustmentAsync(
        Guid driverUserId,
        Guid walletId,
        Guid transactionId,
        decimal amount,
        string direction,
        CancellationToken cancellationToken = default);
}
