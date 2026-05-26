using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Orders.Support;
using Zadana.Application.Modules.Social.Support;
using Zadana.Application.Modules.Wallets.Interfaces;
using Zadana.Api.Realtime.Contracts;
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

            await _hubContext.Clients
                .Group(NotificationHub.GetUserGroup(userId))
                .SendAsync(NotificationHub.ReceiveNotificationMethod, payload, cancellationToken);
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
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedOldStatus = OrderTrackingStatusMapper.NormalizeCustomerTrackingStatus(oldStatus);
            var normalizedNewStatus = OrderTrackingStatusMapper.NormalizeCustomerTrackingStatus(newStatus);
            var payload = new OrderStatusChangedRealtimePayload(
                orderId,
                orderNumber,
                vendorId,
                normalizedOldStatus,
                normalizedNewStatus,
                actorRole,
                string.IsNullOrWhiteSpace(action) ? "status_changed" : action,
                string.IsNullOrWhiteSpace(targetUrl) ? $"/orders/{orderId}" : targetUrl,
                DateTime.UtcNow);

            await _hubContext.Clients
                .Group(NotificationHub.GetUserGroup(userId))
                .SendAsync(NotificationHub.ReceiveOrderStatusChangedMethod, payload, cancellationToken);
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
                DateTime.UtcNow);

            await _hubContext.Clients
                .Group(NotificationHub.GetUserGroup(userId))
                .SendAsync(NotificationHub.ReceiveDriverArrivalStateChangedMethod, payload, cancellationToken);
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
                "support_case_status_update",
                true);

            await _hubContext.Clients
                .Group(NotificationHub.GetUserGroup(userId))
                .SendAsync(NotificationHub.ReceiveOrderSupportCaseChangedMethod, payload, cancellationToken);
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
                DateTime.UtcNow);

            await _hubContext.Clients
                .Group(NotificationHub.GetUserGroup(driverUserId))
                .SendAsync(NotificationHub.ReceiveDriverSupportCaseChangedMethod, payload, cancellationToken);
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
        Guid assignmentId,
        Guid orderId,
        string orderNumber,
        string vendorName,
        decimal deliveryFee,
        decimal totalAmount,
        decimal codAmount,
        string paymentMethod,
        int countdownSeconds,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new Contracts.DeliveryOfferRealtimePayload(
                assignmentId,
                orderId,
                orderNumber,
                vendorName,
                deliveryFee,
                totalAmount,
                codAmount,
                paymentMethod,
                countdownSeconds,
                DateTime.UtcNow);

            _logger.LogInformation(
                "Sending delivery offer SignalR event {Method} to user {UserId}. AssignmentId: {AssignmentId}. OrderId: {OrderId}.",
                NotificationHub.ReceiveDeliveryOfferMethod,
                driverUserId,
                assignmentId,
                orderId);

            await _hubContext.Clients
                .Group(NotificationHub.GetUserGroup(driverUserId))
                .SendAsync(NotificationHub.ReceiveDeliveryOfferMethod, payload, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send delivery offer SignalR event to driver {UserId} for assignment {AssignmentId}",
                driverUserId,
                assignmentId);
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

            await _hubContext.Clients
                .Group("all-customers")
                .SendAsync(NotificationHub.ReceiveBroadcastMethod, payload, cancellationToken);
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

            await _hubContext.Clients
                .Group(NotificationHub.GetUserGroup(driverUserId))
                .SendAsync(NotificationHub.ReceiveAssignmentUpdatedMethod, detail, cancellationToken);

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

            await _hubContext.Clients
                .Group(NotificationHub.GetUserGroup(driverUserId))
                .SendAsync(NotificationHub.ReceiveDriverHomeUpdatedMethod, home, cancellationToken);
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

            await _hubContext.Clients
                .Group(NotificationHub.GetUserGroup(driverUserId))
                .SendAsync(NotificationHub.ReceiveDriverWalletUpdatedMethod, wallet, cancellationToken);
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
        await dbContext.SaveChangesAsync(cancellationToken);

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
