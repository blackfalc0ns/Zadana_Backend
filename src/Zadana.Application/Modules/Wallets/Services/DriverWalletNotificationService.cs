using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Wallets.Support;
using Zadana.Domain.Modules.Social.Enums;
using Zadana.Domain.Modules.Wallets.Entities;

namespace Zadana.Application.Modules.Wallets.Services;

public sealed class DriverWalletNotificationService : IDriverWalletNotificationService
{
    private readonly INotificationService _notificationService;
    private readonly IOneSignalPushService _oneSignalPushService;

    public DriverWalletNotificationService(
        INotificationService notificationService,
        IOneSignalPushService oneSignalPushService)
    {
        _notificationService = notificationService;
        _oneSignalPushService = oneSignalPushService;
    }

    public Task NotifyWithdrawalSubmittedAsync(
        Guid driverUserId,
        DriverWithdrawalRequest withdrawal,
        CancellationToken cancellationToken = default) =>
        DispatchAsync(
            driverUserId,
            withdrawal.Id,
            DriverWalletNotificationComposer.ComposeWithdrawalSubmitted(withdrawal),
            NotificationPriorities.Normal,
            cancellationToken);

    public Task NotifyWithdrawalCancelledAsync(
        Guid driverUserId,
        DriverWithdrawalRequest withdrawal,
        CancellationToken cancellationToken = default) =>
        DispatchAsync(
            driverUserId,
            withdrawal.Id,
            DriverWalletNotificationComposer.ComposeWithdrawalCancelled(withdrawal),
            NotificationPriorities.Normal,
            cancellationToken);

    public Task NotifyWithdrawalProcessingAsync(
        Guid driverUserId,
        DriverWithdrawalRequest withdrawal,
        CancellationToken cancellationToken = default) =>
        DispatchAsync(
            driverUserId,
            withdrawal.Id,
            DriverWalletNotificationComposer.ComposeWithdrawalProcessing(withdrawal),
            NotificationPriorities.High,
            cancellationToken);

    public Task NotifyWithdrawalRejectedAsync(
        Guid driverUserId,
        DriverWithdrawalRequest withdrawal,
        CancellationToken cancellationToken = default) =>
        DispatchAsync(
            driverUserId,
            withdrawal.Id,
            DriverWalletNotificationComposer.ComposeWithdrawalRejected(withdrawal),
            NotificationPriorities.High,
            cancellationToken);

    public Task NotifyWithdrawalFailedAsync(
        Guid driverUserId,
        DriverWithdrawalRequest withdrawal,
        CancellationToken cancellationToken = default) =>
        DispatchAsync(
            driverUserId,
            withdrawal.Id,
            DriverWalletNotificationComposer.ComposeWithdrawalFailed(withdrawal),
            NotificationPriorities.High,
            cancellationToken);

    public Task NotifyWithdrawalPaidAsync(
        Guid driverUserId,
        DriverWithdrawalRequest withdrawal,
        CancellationToken cancellationToken = default) =>
        DispatchAsync(
            driverUserId,
            withdrawal.Id,
            DriverWalletNotificationComposer.ComposeWithdrawalPaid(withdrawal),
            NotificationPriorities.High,
            cancellationToken);

    public Task NotifyWithdrawalReturnedAsync(
        Guid driverUserId,
        DriverWithdrawalRequest withdrawal,
        Guid payoutId,
        decimal amount,
        string? reason,
        CancellationToken cancellationToken = default) =>
        DispatchAsync(
            driverUserId,
            withdrawal.Id,
            DriverWalletNotificationComposer.ComposeWithdrawalReturned(withdrawal, payoutId, amount, reason),
            NotificationPriorities.High,
            cancellationToken);

    public Task NotifyAdminWalletAdjustmentAsync(
        Guid driverUserId,
        Guid walletId,
        Guid transactionId,
        decimal amount,
        string direction,
        CancellationToken cancellationToken = default) =>
        DispatchAsync(
            driverUserId,
            transactionId,
            DriverWalletNotificationComposer.ComposeAdminWalletAdjustment(walletId, transactionId, amount, direction),
            NotificationPriorities.Normal,
            cancellationToken);

    private async Task DispatchAsync(
        Guid driverUserId,
        Guid referenceId,
        DriverWalletNotificationContent content,
        string priority,
        CancellationToken cancellationToken)
    {
        await _notificationService.SendToUserAsync(
            driverUserId,
            new NotificationDispatchRequest(
                content.TitleAr,
                content.TitleEn,
                content.BodyAr,
                content.BodyEn,
                NotificationTypes.DriverWalletUpdated,
                NotificationCategories.Wallet,
                priority,
                referenceId,
                content.Data),
            cancellationToken);

        await _notificationService.SendDriverWalletUpdatedAsync(driverUserId, cancellationToken);

        await _oneSignalPushService.SendMobileNotificationAsync(
            OneSignalMobilePushRequest.CreateHeadsUp(
                driverUserId.ToString(),
                content.TitleAr,
                content.TitleEn,
                content.BodyAr,
                content.BodyEn,
                NotificationTypes.DriverWalletUpdated,
                referenceId,
                content.Data,
                content.TargetUrl,
                NotificationCategories.Wallet,
                OneSignalApplicationTarget.Driver),
            cancellationToken);
    }
}
