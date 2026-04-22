using Microsoft.Extensions.Logging;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Orders.Support;

namespace Zadana.Application.Modules.Orders.Services;

public sealed class OrderStatusNotificationDispatcher : IOrderStatusNotificationDispatcher
{
    private readonly INotificationService _notificationService;
    private readonly IOneSignalPushService _oneSignalPushService;
    private readonly ILogger<OrderStatusNotificationDispatcher> _logger;

    public OrderStatusNotificationDispatcher(
        INotificationService notificationService,
        IOneSignalPushService oneSignalPushService,
        ILogger<OrderStatusNotificationDispatcher> logger)
    {
        _notificationService = notificationService;
        _oneSignalPushService = oneSignalPushService;
        _logger = logger;
    }

    public async Task<OrderStatusNotificationDispatchResult> DispatchCustomerAsync(
        OrderStatusCustomerNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var composed = OrderStatusNotificationComposer.ComposeCustomer(
            request.OrderId,
            request.VendorId,
            request.OrderNumber,
            request.OldStatus,
            request.NewStatus,
            request.ActorRole);

        var inboxQueued = false;
        var realtimeQueued = false;

        _logger.LogInformation(
            "Dispatching customer order-status notification for order {OrderId} user {UserId} from {OldStatus} to {NewStatus}",
            request.OrderId,
            request.UserId,
            request.OldStatus,
            request.NewStatus);

        try
        {
            await _notificationService.SendToUserAsync(
                request.UserId,
                composed.TitleAr,
                composed.TitleEn,
                composed.BodyAr,
                composed.BodyEn,
                composed.NotificationType,
                request.OrderId,
                composed.Data,
                cancellationToken);
            inboxQueued = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to queue inbox notification for order {OrderId} user {UserId}",
                request.OrderId,
                request.UserId);
        }

        try
        {
            await _notificationService.SendOrderStatusChangedToUserAsync(
                request.UserId,
                request.OrderId,
                request.OrderNumber,
                request.VendorId,
                request.OldStatus.ToString(),
                request.NewStatus.ToString(),
                request.ActorRole,
                composed.Action,
                composed.TargetUrl,
                cancellationToken);
            realtimeQueued = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to queue realtime order-status notification for order {OrderId} user {UserId}",
                request.OrderId,
                request.UserId);
        }

        OneSignalPushDispatchResult pushResult;
        try
        {
            pushResult = await _oneSignalPushService.SendToExternalUserAsync(
                request.UserId.ToString(),
                composed.TitleAr,
                composed.TitleEn,
                composed.BodyAr,
                composed.BodyEn,
                composed.NotificationType,
                request.OrderId,
                composed.Data,
                composed.TargetUrl,
                OneSignalPushProfile.MobileHeadsUp,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Order-status push dispatch threw unexpectedly for order {OrderId} user {UserId}",
                request.OrderId,
                request.UserId);
            pushResult = new OneSignalPushDispatchResult(
                Attempted: true,
                Sent: false,
                Skipped: false,
                ProviderStatusCode: null,
                ProviderNotificationId: null,
                Reason: ex.Message);
        }

        if (!pushResult.Sent && !pushResult.Skipped)
        {
            _logger.LogWarning(
                "Customer order-status push failed for order {OrderId} user {UserId}. ProviderStatusCode {ProviderStatusCode}. Reason: {Reason}",
                request.OrderId,
                request.UserId,
                pushResult.ProviderStatusCode,
                pushResult.Reason);
        }

        _logger.LogInformation(
            "Customer order-status notification dispatch completed for order {OrderId} user {UserId}. InboxQueued {InboxQueued}. RealtimeQueued {RealtimeQueued}. PushAttempted {PushAttempted}. PushSent {PushSent}. ProviderStatusCode {ProviderStatusCode}",
            request.OrderId,
            request.UserId,
            inboxQueued,
            realtimeQueued,
            pushResult.Attempted,
            pushResult.Sent,
            pushResult.ProviderStatusCode);

        return new OrderStatusNotificationDispatchResult(
            inboxQueued,
            realtimeQueued,
            pushResult.Attempted,
            pushResult.Sent,
            pushResult.ProviderStatusCode,
            pushResult.Reason);
    }
}
