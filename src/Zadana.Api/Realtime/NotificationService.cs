using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Orders.Support;
using Zadana.Application.Modules.Social.Support;
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
        string titleAr,
        string titleEn,
        string bodyAr,
        string bodyEn,
        string? type = null,
        Guid? referenceId = null,
        string? data = null,
        CancellationToken cancellationToken = default)
    {
        var sanitized = NotificationPayloadHelper.Sanitize(titleAr, titleEn, bodyAr, bodyEn, type, data);

        await PersistNotificationAsync(
            userId,
            sanitized.TitleAr,
            sanitized.TitleEn,
            sanitized.BodyAr,
            sanitized.BodyEn,
            sanitized.Type,
            referenceId,
            sanitized.Data,
            cancellationToken);
    }

    public async Task SendToUserAsync(
        Guid userId,
        string titleAr,
        string titleEn,
        string bodyAr,
        string bodyEn,
        string? type = null,
        Guid? referenceId = null,
        string? data = null,
        CancellationToken cancellationToken = default)
    {
        var sanitized = NotificationPayloadHelper.Sanitize(titleAr, titleEn, bodyAr, bodyEn, type, data);

        Guid notificationId;
        DateTime createdAtUtc;
        try
        {
            (notificationId, createdAtUtc) = await PersistNotificationAsync(
                userId,
                sanitized.TitleAr,
                sanitized.TitleEn,
                sanitized.BodyAr,
                sanitized.BodyEn,
                sanitized.Type,
                referenceId,
                sanitized.Data,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist notification for user {UserId}", userId);
            notificationId = Guid.NewGuid();
            createdAtUtc = DateTime.UtcNow;
        }

        // Send real-time via SignalR after the inbox record is persisted.
        try
        {
            var payload = new NotificationPayload(
                notificationId,
                sanitized.TitleAr,
                sanitized.TitleEn,
                sanitized.BodyAr,
                sanitized.BodyEn,
                sanitized.Type,
                referenceId,
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

    public async Task SendDeliveryOfferToDriverAsync(
        Guid driverUserId,
        Guid assignmentId,
        Guid orderId,
        string orderNumber,
        string vendorName,
        decimal deliveryFee,
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
                countdownSeconds,
                DateTime.UtcNow);

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

    private async Task<(Guid NotificationId, DateTime CreatedAtUtc)> PersistNotificationAsync(
        Guid userId,
        string titleAr,
        string titleEn,
        string bodyAr,
        string bodyEn,
        string? type,
        Guid? referenceId,
        string? data,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var notification = new Notification(
            userId,
            titleAr,
            titleEn,
            bodyAr,
            bodyEn,
            type,
            referenceId,
            data);

        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync(cancellationToken);

        return (notification.Id, notification.CreatedAtUtc);
    }
}

public sealed record NotificationPayload(
    Guid Id,
    string TitleAr,
    string TitleEn,
    string BodyAr,
    string BodyEn,
    string? Type,
    Guid? ReferenceId,
    string? Data,
    System.Text.Json.JsonElement? DataObject,
    bool IsRead,
    DateTime CreatedAtUtc);
