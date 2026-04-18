using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Social.Support;
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

        // 1. Persist to database
        Guid notificationId;
        DateTime createdAtUtc;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var notification = new Notification(
                userId,
                sanitized.TitleAr,
                sanitized.TitleEn,
                sanitized.BodyAr,
                sanitized.BodyEn,
                sanitized.Type,
                referenceId,
                sanitized.Data);
            dbContext.Notifications.Add(notification);
            await dbContext.SaveChangesAsync(cancellationToken);

            notificationId = notification.Id;
            createdAtUtc = notification.CreatedAtUtc;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist notification for user {UserId}", userId);
            notificationId = Guid.NewGuid();
            createdAtUtc = DateTime.UtcNow;
        }

        // 2. Send real-time via SignalR
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
                .SendAsync("ReceiveNotification", payload, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SignalR notification to user {UserId}", userId);
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
                .SendAsync("ReceiveBroadcast", payload, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast notification to all customers");
        }
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
