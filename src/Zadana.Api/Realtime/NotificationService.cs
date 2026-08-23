using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Orders.Support;
using Zadana.Application.Modules.Social.Support;
using Zadana.Application.Modules.Wallets.Interfaces;
using Zadana.Api.Realtime.Contracts;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Social.Entities;
using Zadana.Infrastructure.Persistence;

namespace Zadana.Api.Realtime;

public sealed class NotificationService : INotificationService
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IHubContext<NotificationHub> hubContext,
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationService> logger)
    {
        _hubContext = hubContext;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task PersistToUserAsync(
        Guid userId,
        NotificationDispatchRequest request,
        CancellationToken cancellationToken = default)
    {
        var sanitized = SanitizeRequest(request);
        await PersistNotificationAsync(userId, sanitized, cancellationToken);
    }

    public Task PersistToUserAsync(
        Guid userId,
        string titleAr,
        string titleEn,
        string bodyAr,
        string bodyEn,
        string? type = null,
        Guid? referenceId = null,
        string? data = null,
        CancellationToken cancellationToken = default) =>
        PersistToUserAsync(
            userId,
            new NotificationDispatchRequest(
                titleAr,
                titleEn,
                bodyAr,
                bodyEn,
                type,
                ReferenceId: referenceId,
                Data: data),
            cancellationToken);

    public async Task SendToUserAsync(
        Guid userId,
        NotificationDispatchRequest request,
        CancellationToken cancellationToken = default)
    {
        var sanitized = SanitizeRequest(request);

        Guid notificationId;
        DateTime createdAtUtc;
        try
        {
            (notificationId, createdAtUtc) = await PersistNotificationAsync(
                userId,
                sanitized,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist notification for user {UserId}", userId);
            notificationId = Guid.NewGuid();
            createdAtUtc = DateTime.UtcNow;
        }

        try
        {
            var payload = new NotificationPayload(
                notificationId,
                sanitized.TitleAr,
                sanitized.TitleEn,
                sanitized.BodyAr,
                sanitized.BodyEn,
                sanitized.Type,
                sanitized.Category,
                sanitized.Priority,
                sanitized.ReferenceId,
                sanitized.Data,
                sanitized.DataObject,
                false,
                createdAtUtc);

            await SendHubAsync(
                NotificationHub.GetUserGroup(userId),
                NotificationHub.ReceiveNotificationMethod,
                payload,
                "inbox",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SignalR notification to user {UserId}", userId);
        }
    }

    public Task SendToUserAsync(
        Guid userId,
        string titleAr,
        string titleEn,
        string bodyAr,
        string bodyEn,
        string? type = null,
        Guid? referenceId = null,
        string? data = null,
        CancellationToken cancellationToken = default) =>
        SendToUserAsync(
            userId,
            new NotificationDispatchRequest(
                titleAr,
                titleEn,
                bodyAr,
                bodyEn,
                type,
                ReferenceId: referenceId,
                Data: data),
            cancellationToken);

    public async Task SendOrderStatusChangedToUserAsync(
        Guid userId,
        Guid orderId,
        string orderNumber,
        Guid vendorId,
        string oldStatus,
        string newStatus,
        string? actorRole = null,
        string? action = null,
        string? targetUrl = null,
        CancellationToken cancellationToken = default,
        string? fulfillmentType = null,
        bool showPopup = false)
    {
        try
        {
            var fulfillment = ResolveFulfillmentType(fulfillmentType);
            var normalizedOldStatus = OrderTrackingStatusMapper.NormalizeCustomerTrackingStatus(oldStatus, fulfillment);
            var normalizedNewStatus = OrderTrackingStatusMapper.NormalizeCustomerTrackingStatus(newStatus, fulfillment);
            var normalizedFulfillment = fulfillment == FulfillmentType.Pickup ? "pickup" : "delivery";
            var payload = new OrderStatusChangedRealtimePayload(
                orderId,
                orderNumber,
                vendorId,
                normalizedOldStatus,
                normalizedNewStatus,
                actorRole,
                string.IsNullOrWhiteSpace(action) ? "status_changed" : action,
                string.IsNullOrWhiteSpace(targetUrl) ? $"/orders/{orderId}" : targetUrl,
                DateTime.UtcNow,
                showPopup ? "popup" : "silent",
                ResolveOrderStatusPopupType(newStatus, fulfillment),
                showPopup,
                oldStatus,
                newStatus,
                normalizedFulfillment);

            await SendHubAsync(
                NotificationHub.GetUserGroup(userId),
                NotificationHub.ReceiveOrderStatusChangedMethod,
                payload,
                "order-status",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send order status SignalR event to user {UserId} for order {OrderId}",
                userId,
                orderId);
        }
    }

    private static FulfillmentType ResolveFulfillmentType(string? fulfillmentType) =>
        string.Equals(fulfillmentType, "pickup", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(fulfillmentType, nameof(FulfillmentType.Pickup), StringComparison.OrdinalIgnoreCase)
            ? FulfillmentType.Pickup
            : FulfillmentType.Delivery;

    private static string ResolveOrderStatusPopupType(string newStatus, FulfillmentType fulfillment)
    {
        if (string.Equals(newStatus, nameof(OrderStatus.Refunded), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(newStatus, "refunded", StringComparison.OrdinalIgnoreCase))
        {
            return "order_refund_status_changed";
        }

        if (fulfillment == FulfillmentType.Pickup &&
            (string.Equals(newStatus, nameof(OrderStatus.ReadyForPickup), StringComparison.OrdinalIgnoreCase) ||
             string.Equals(newStatus, "ready_for_pickup", StringComparison.OrdinalIgnoreCase)))
        {
            return "order_pickup_ready";
        }

        return "order_status_changed";
    }

    public async Task SendDriverArrivalStateChangedToUserAsync(
        Guid userId,
        Guid orderId,
        string orderNumber,
        string arrivalState,
        string driverName,
        string? actorRole = null,
        string? targetUrl = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new DriverArrivalStateChangedRealtimePayload(
                orderId,
                orderNumber,
                arrivalState,
                driverName,
                actorRole,
                string.IsNullOrWhiteSpace(targetUrl) ? $"/orders/{orderId}" : targetUrl,
                DateTime.UtcNow,
                "popup",
                "driver_arrival_state_changed",
                true);

            await SendHubAsync(
                NotificationHub.GetUserGroup(userId),
                NotificationHub.ReceiveDriverArrivalStateChangedMethod,
                payload,
                "driver-arrival",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send driver arrival SignalR event to user {UserId} for order {OrderId}",
                userId,
                orderId);
        }
    }

    public async Task SendOrderSupportCaseChangedToUserAsync(
        Guid userId,
        Guid caseId,
        Guid orderId,
        string orderNumber,
        string type,
        string status,
        string action,
        string? targetUrl = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new OrderSupportCaseChangedRealtimePayload(
                caseId,
                orderId,
                orderNumber,
                type,
                status,
                action,
                string.IsNullOrWhiteSpace(targetUrl) ? $"/orders/{orderId}/cases/{caseId}" : targetUrl,
                DateTime.UtcNow,
                "popup",
                ResolveOrderSupportPopupType(type),
                true);

            await SendHubAsync(
                NotificationHub.GetUserGroup(userId),
                NotificationHub.ReceiveOrderSupportCaseChangedMethod,
                payload,
                "order-support",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send support-case SignalR event to user {UserId} for case {CaseId}",
                userId,
                caseId);
        }
    }

    private static string ResolveOrderSupportPopupType(string type) =>
        string.Equals(type, "return_request", StringComparison.OrdinalIgnoreCase)
            ? "return_request_status_update"
            : "support_case_status_update";

    public async Task SendDriverSupportCaseChangedToUserAsync(
        Guid driverUserId,
        Guid caseId,
        Guid? driverId,
        Guid? orderId,
        string? orderNumber,
        string type,
        string status,
        string action,
        string? targetUrl = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new DriverSupportCaseChangedRealtimePayload(
                caseId,
                driverId,
                orderId,
                orderNumber,
                type,
                status,
                action,
                string.IsNullOrWhiteSpace(targetUrl)
                    ? orderId.HasValue ? $"/orders/{orderId}/cases/{caseId}" : $"/support/cases/{caseId}"
                    : targetUrl,
                DateTime.UtcNow,
                "popup",
                "support_case_status_update",
                true);

            await SendHubAsync(
                NotificationHub.GetUserGroup(driverUserId),
                NotificationHub.ReceiveDriverSupportCaseChangedMethod,
                payload,
                "driver-support",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send driver support-case SignalR event to user {UserId} for case {CaseId}",
                driverUserId,
                caseId);
        }
    }

    public async Task SendDeliveryOfferToDriverAsync(
        Guid driverUserId,
        Application.Modules.Delivery.DTOs.DriverIncomingOfferDto currentOffer,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new Contracts.DeliveryOfferRealtimePayload(
                currentOffer,
                DateTime.UtcNow);

            _logger.LogInformation(
                "Sending delivery offer SignalR event {Method} to user {UserId}. AssignmentId: {AssignmentId}. OrderId: {OrderId}.",
                NotificationHub.ReceiveDeliveryOfferMethod,
                driverUserId,
                currentOffer.AssignmentId,
                currentOffer.OrderId);

            await SendHubAsync(
                NotificationHub.GetUserGroup(driverUserId),
                NotificationHub.ReceiveDeliveryOfferMethod,
                payload,
                "delivery-offer",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send delivery offer SignalR event to driver {UserId} for assignment {AssignmentId}",
                driverUserId,
                currentOffer.AssignmentId);
        }
    }

    public async Task BroadcastToAllCustomersAsync(
        string titleAr,
        string titleEn,
        string bodyAr,
        string bodyEn,
        string? type = null,
        string? data = null,
        CancellationToken cancellationToken = default)
    {
        var sanitized = NotificationPayloadHelper.Sanitize(titleAr, titleEn, bodyAr, bodyEn, type, data);

        try
        {
            var payload = new NotificationPayload(
                Guid.NewGuid(),
                sanitized.TitleAr,
                sanitized.TitleEn,
                sanitized.BodyAr,
                sanitized.BodyEn,
                sanitized.Type,
                null,
                null,
                null,
                sanitized.Data,
                sanitized.DataObject,
                false,
                DateTime.UtcNow);

            await SendHubAsync(
                "all-customers",
                NotificationHub.ReceiveBroadcastMethod,
                payload,
                "broadcast",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast notification to all customers");
        }
    }

    public async Task SendAssignmentUpdatedToDriverAsync(
        Guid driverUserId,
        Guid assignmentId,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var driverReadService = scope.ServiceProvider.GetRequiredService<Application.Modules.Delivery.Interfaces.IDriverReadService>();
            var driverRepository = scope.ServiceProvider.GetRequiredService<Application.Modules.Delivery.Interfaces.IDriverRepository>();

            var driver = await driverRepository.GetByUserIdAsync(driverUserId, cancellationToken);
            if (driver is null)
            {
                _logger.LogWarning("SendAssignmentUpdatedToDriverAsync: no driver found for user {UserId}", driverUserId);
                return;
            }

            var detail = await driverReadService.GetAssignmentDetailAsync(driver.Id, assignmentId, cancellationToken);
            if (detail is null)
            {
                _logger.LogWarning(
                    "SendAssignmentUpdatedToDriverAsync: assignment {AssignmentId} not found for driver {DriverId}",
                    assignmentId, driver.Id);
                return;
            }

            await SendHubAsync(
                NotificationHub.GetUserGroup(driverUserId),
                NotificationHub.ReceiveAssignmentUpdatedMethod,
                detail,
                "assignment-updated",
                cancellationToken);

            _logger.LogInformation(
                "Sent ReceiveAssignmentUpdated to driver user {UserId} for assignment {AssignmentId}",
                driverUserId, assignmentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send assignment update to driver user {UserId} for assignment {AssignmentId}",
                driverUserId, assignmentId);
        }
    }

    public async Task SendDriverHomeUpdatedAsync(
        Guid driverUserId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var driverHomeReadService = scope.ServiceProvider.GetRequiredService<IDriverHomeReadService>();
            var home = await driverHomeReadService.GetHomeAsync(driverUserId, processExpiredOffers: false, cancellationToken);

            await SendHubAsync(
                NotificationHub.GetUserGroup(driverUserId),
                NotificationHub.ReceiveDriverHomeUpdatedMethod,
                home,
                "driver-home",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send driver home update to driver user {UserId}",
                driverUserId);
        }
    }

    public async Task SendDriverWalletUpdatedAsync(
        Guid driverUserId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var driverWalletReadService = scope.ServiceProvider.GetRequiredService<IDriverWalletReadService>();
            var wallet = await driverWalletReadService.GetRealtimePayloadAsync(driverUserId, cancellationToken);

            await SendHubAsync(
                NotificationHub.GetUserGroup(driverUserId),
                NotificationHub.ReceiveDriverWalletUpdatedMethod,
                wallet,
                "driver-wallet",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send driver wallet update to driver user {UserId}",
                driverUserId);
        }
    }

    private async Task<(Guid NotificationId, DateTime CreatedAtUtc)> PersistNotificationAsync(
        Guid userId,
        SanitizedNotificationDispatchRequest request,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var notification = new Notification(
            userId,
            request.TitleAr,
            request.TitleEn,
            request.BodyAr,
            request.BodyEn,
            request.Type,
            request.Category,
            request.Priority,
            request.ReferenceId,
            request.Data);

        dbContext.Notifications.Add(notification);

        using var persistCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        persistCts.CancelAfter(TimeSpan.FromSeconds(3));
        await dbContext.SaveChangesAsync(persistCts.Token);

        return (notification.Id, notification.CreatedAtUtc);
    }

    private static SanitizedNotificationDispatchRequest SanitizeRequest(NotificationDispatchRequest request)
    {
        var sanitized = NotificationPayloadHelper.Sanitize(
            request.TitleAr,
            request.TitleEn,
            request.BodyAr,
            request.BodyEn,
            request.Type,
            request.Data);

        return new SanitizedNotificationDispatchRequest(
            sanitized.TitleAr,
            sanitized.TitleEn,
            sanitized.BodyAr,
            sanitized.BodyEn,
            sanitized.Type,
            NormalizeToken(request.Category),
            NormalizeToken(request.Priority),
            request.ReferenceId,
            sanitized.Data,
            sanitized.DataObject);
    }

    private Task SendHubAsync(
        string groupName,
        string methodName,
        object payload,
        string operation,
        CancellationToken cancellationToken) =>
        SignalRDispatch.SendToGroupAsync(
            _hubContext,
            groupName,
            methodName,
            payload,
            _logger,
            operation,
            cancellationToken);

    private static string? NormalizeToken(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record NotificationPayload(
    Guid Id,
    string TitleAr,
    string TitleEn,
    string BodyAr,
    string BodyEn,
    string? Type,
    string? Category,
    string? Priority,
    Guid? ReferenceId,
    string? Data,
    System.Text.Json.JsonElement? DataObject,
    bool IsRead,
    DateTime CreatedAtUtc);

internal sealed record SanitizedNotificationDispatchRequest(
    string TitleAr,
    string TitleEn,
    string BodyAr,
    string BodyEn,
    string? Type,
    string? Category,
    string? Priority,
    Guid? ReferenceId,
    string? Data,
    System.Text.Json.JsonElement? DataObject);
