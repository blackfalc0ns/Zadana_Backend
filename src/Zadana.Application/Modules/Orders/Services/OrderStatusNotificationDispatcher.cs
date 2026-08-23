using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Orders.Support;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Social.Enums;

namespace Zadana.Application.Modules.Orders.Services;

public sealed class OrderStatusNotificationDispatcher : IOrderStatusNotificationDispatcher
{
    private readonly INotificationService _notificationService;
    private readonly IOneSignalPushService _oneSignalPushService;
    private readonly ICustomerPresenceService _customerPresenceService;
    private readonly IApplicationDbContext _context;
    private readonly ILogger<OrderStatusNotificationDispatcher> _logger;

    public OrderStatusNotificationDispatcher(
        INotificationService notificationService,
        IOneSignalPushService oneSignalPushService,
        ICustomerPresenceService customerPresenceService,
        IApplicationDbContext context,
        ILogger<OrderStatusNotificationDispatcher> logger)
    {
        _notificationService = notificationService;
        _oneSignalPushService = oneSignalPushService;
        _customerPresenceService = customerPresenceService;
        _context = context;
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
            request.ActorRole,
            request.Fulfillment);

        if (composed is null)
        {
            _logger.LogInformation(
                "Skipping customer order-status notification for order {OrderId} user {UserId}: status {NewStatus} is outside the customer notify whitelist.",
                request.OrderId,
                request.UserId,
                request.NewStatus);

            return new OrderStatusNotificationDispatchResult(
                InboxQueued: false,
                RealtimeQueued: false,
                PushAttempted: false,
                PushSent: false,
                PushProviderStatusCode: null,
                PushReason: $"Status {request.NewStatus} is not customer-notified.");
        }

        var pushRequest = BuildCustomerMobilePushRequest(request, composed);
        var dedupeKey = OrderStatusCustomerNotificationDedupe.TryExtractDedupeKey(composed.Data);

        if (!string.IsNullOrWhiteSpace(dedupeKey) &&
            await HasRecentDuplicateAsync(request.UserId, dedupeKey, cancellationToken))
        {
            _logger.LogInformation(
                "Skipping duplicate customer order-status notification for order {OrderId} user {UserId} status {NewStatus} (dedupeKey {DedupeKey}).",
                request.OrderId,
                request.UserId,
                request.NewStatus,
                dedupeKey);

            return new OrderStatusNotificationDispatchResult(
                InboxQueued: false,
                RealtimeQueued: false,
                PushAttempted: false,
                PushSent: false,
                PushProviderStatusCode: null,
                PushReason: $"Duplicate suppressed for {dedupeKey}.");
        }

        var inboxQueued = false;

        _logger.LogInformation(
            "Dispatching customer order-status notification for order {OrderId} user {UserId} from {OldStatus} to {NewStatus}",
            request.OrderId,
            request.UserId,
            request.OldStatus,
            request.NewStatus);

        try
        {
            // Inbox only — no ReceiveNotification SignalR event. OneSignal push owns the visible banner;
            // ReceiveNotification duplicated the push and ReceiveOrderStatusChanged duplicated tracking UI popups.
            await _notificationService.PersistToUserAsync(
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
                "Failed to persist inbox notification for order {OrderId} user {UserId}",
                request.OrderId,
                request.UserId);
        }

        // Inbox persist only + one OneSignal push. No ReceiveNotification / ReceiveOrderStatusChanged
        // (those made the customer app show "تحديث على الطلب" + duplicate status banners).
        const bool realtimeQueued = false;

        var isForeground = _customerPresenceService.IsOnline(request.UserId);
        var shouldSuppressPush = ShouldSuppressForegroundPush(request, isForeground);

        _logger.LogWarning(
            "[PUSH-DIAG] About to send OneSignal push for order {OrderId}. ExternalId: {ExternalId}. PushType: {PushType}. BusinessType: {BusinessType}. TitleEn: {TitleEn}. BodyEn: {BodyEn}. Profile: {Profile}. TargetUrl: {TargetUrl}. RealtimeSuppressed: {RealtimeSuppressed}",
            request.OrderId,
            pushRequest.ExternalUserId,
            pushRequest.Type,
            composed.NotificationType,
            pushRequest.TitleEn,
            pushRequest.BodyEn,
            pushRequest.Profile,
            pushRequest.TargetUrl,
            shouldSuppressPush);

        OneSignalPushDispatchResult pushResult;
        try
        {
            if (shouldSuppressPush)
            {
                pushResult = new OneSignalPushDispatchResult(
                    Attempted: false,
                    Sent: false,
                    Skipped: true,
                    ProviderStatusCode: null,
                    ProviderNotificationId: null,
                    Reason: "Customer is active in the foreground; SignalR delivery suppresses duplicate push.");
            }
            else
            {
                pushResult = request.NewStatus is OrderStatus.OnTheWay or OrderStatus.Delivered
                    ? await _oneSignalPushService.SendMobileNotificationDirectAsync(pushRequest, cancellationToken)
                    : await pushRequest.DispatchAsync(_oneSignalPushService, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[PUSH-DIAG] Order-status push dispatch threw unexpectedly for order {OrderId} user {UserId}",
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

        _logger.LogWarning(
            "[PUSH-DIAG] OneSignal push result for order {OrderId}. ExternalId: {ExternalId}. PushType: {PushType}. BusinessType: {BusinessType}. Attempted: {Attempted}. Sent: {Sent}. Skipped: {Skipped}. ProviderStatusCode: {ProviderStatusCode}. ProviderNotificationId: {ProviderNotificationId}. Reason: {Reason}",
            request.OrderId,
            pushRequest.ExternalUserId,
            pushRequest.Type,
            composed.NotificationType,
            pushResult.Attempted,
            pushResult.Sent,
            pushResult.Skipped,
            pushResult.ProviderStatusCode,
            pushResult.ProviderNotificationId,
            pushResult.Reason);

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
            "Customer order-status notification dispatch completed for order {OrderId} user {UserId}. InboxQueued {InboxQueued}. RealtimeQueued {RealtimeQueued}. PushAttempted {PushAttempted}. PushSent {PushSent}. ProviderStatusCode {ProviderStatusCode}. ProviderNotificationId {ProviderNotificationId}",
            request.OrderId,
            request.UserId,
            inboxQueued,
            realtimeQueued,
            pushResult.Attempted,
            pushResult.Sent,
            pushResult.ProviderStatusCode,
            pushResult.ProviderNotificationId);

        return new OrderStatusNotificationDispatchResult(
            inboxQueued,
            realtimeQueued,
            pushResult.Attempted,
            pushResult.Sent,
            pushResult.ProviderStatusCode,
            pushResult.Reason);
    }

    /// <summary>
    /// Only suppress low-priority status noise while the app is foregrounded.
    /// Heads-up statuses (pickup ready / delivered / cancelled / on the way) always push
    /// so mobile still gets a popup when SignalR is flaky or the screen is covered.
    /// </summary>
    private static bool ShouldSuppressForegroundPush(
        OrderStatusCustomerNotificationRequest request,
        bool isForeground)
    {
        if (!isForeground)
        {
            return false;
        }

        if (request.Fulfillment == FulfillmentType.Pickup &&
            request.NewStatus is OrderStatus.ReadyForPickup or OrderStatus.Delivered
                or OrderStatus.Cancelled or OrderStatus.VendorRejected or OrderStatus.Refunded)
        {
            return false;
        }

        return request.NewStatus is OrderStatus.Accepted or OrderStatus.Preparing
            or OrderStatus.PendingVendorAcceptance or OrderStatus.Placed;
    }

    private static OneSignalMobilePushRequest BuildCustomerMobilePushRequest(
        OrderStatusCustomerNotificationRequest request,
        CustomerOrderStatusNotification composed) =>
        OneSignalMobilePushRequest.CreateHeadsUp(
            request.UserId.ToString(),
            composed.TitleAr,
            composed.TitleEn,
            composed.BodyAr,
            composed.BodyEn,
            composed.NotificationType,
            request.OrderId,
            composed.Data,
            composed.TargetUrl,
            category: NotificationCategories.Order,
            targetApplication: OneSignalApplicationTarget.Customer);

    private async Task<bool> HasRecentDuplicateAsync(
        Guid userId,
        string dedupeKey,
        CancellationToken cancellationToken)
    {
        var marker = $"\"dedupeKey\":\"{dedupeKey}\"";
        return await _context.Notifications
            .AsNoTracking()
            .AnyAsync(
                notification =>
                    notification.UserId == userId &&
                    notification.Data != null &&
                    notification.Data.Contains(marker),
                cancellationToken);
    }
}
