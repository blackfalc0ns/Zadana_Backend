using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Zadana.Api.Realtime;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Social.Entities;
using Zadana.Domain.Modules.Social.Enums;
using Zadana.Infrastructure.Persistence;

namespace Zadana.Api.BackgroundJobs;

public sealed class AdminAlertOutboxWorker : BackgroundService
{
    private const int BatchSize = 20;
    private const int MaxAttempts = 6;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromHours(1)
    ];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<AdminAlertOutboxWorker> _logger;

    public AdminAlertOutboxWorker(
        IServiceScopeFactory scopeFactory,
        IHubContext<NotificationHub> hubContext,
        ILogger<AdminAlertOutboxWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AdminAlertOutboxWorker starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessReadyEventsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AdminAlertOutboxWorker loop failed.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task ProcessReadyEventsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var pushService = scope.ServiceProvider.GetRequiredService<IOneSignalPushService>();
        var adminAlertService = scope.ServiceProvider.GetRequiredService<IAdminAlertService>();
        var now = DateTime.UtcNow;

        var events = await context.AdminAlertEvents
            .Include(item => item.Dispatches)
            .Where(item =>
                (item.Status == AdminAlertEventStatus.Pending || item.Status == AdminAlertEventStatus.FailedRetryable) &&
                (!item.NextAttemptAtUtc.HasValue || item.NextAttemptAtUtc <= now))
            .OrderBy(item => item.CreatedAtUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var alertEvent in events)
        {
            await ProcessEventAsync(context, pushService, adminAlertService, alertEvent, cancellationToken);
        }
    }

    private async Task ProcessEventAsync(
        ApplicationDbContext context,
        IOneSignalPushService pushService,
        IAdminAlertService adminAlertService,
        AdminAlertEvent alertEvent,
        CancellationToken cancellationToken)
    {
        alertEvent.MarkProcessing();
        await context.SaveChangesAsync(cancellationToken);

        try
        {
            var recipientIds = await context.Users
                .AsNoTracking()
                .Where(user =>
                    user.AccountStatus == AccountStatus.Active &&
                    !user.IsLoginLocked &&
                    (user.Role == UserRole.Admin || user.Role == UserRole.SuperAdmin))
                .Select(user => user.Id)
                .ToListAsync(cancellationToken);

            foreach (var recipientId in recipientIds)
            {
                var dispatch = alertEvent.Dispatches.FirstOrDefault(item => item.AdminUserId == recipientId);
                if (dispatch is null)
                {
                    dispatch = new AdminAlertDispatch(alertEvent.Id, recipientId);
                    alertEvent.Dispatches.Add(dispatch);
                    context.AdminAlertDispatches.Add(dispatch);
                }

                if (!dispatch.NotificationId.HasValue)
                {
                    var notification = new Notification(
                        recipientId,
                        alertEvent.TitleAr,
                        alertEvent.TitleEn,
                        alertEvent.BodyAr,
                        alertEvent.BodyEn,
                        alertEvent.Type,
                        alertEvent.Category,
                        alertEvent.Priority,
                        alertEvent.ReferenceId,
                        alertEvent.DataJson);

                    context.Notifications.Add(notification);
                    await context.SaveChangesAsync(cancellationToken);
                    dispatch.MarkPersisted(notification.Id);
                }

                if (!dispatch.SignalRSent && dispatch.NotificationId.HasValue)
                {
                    await SendSignalRAsync(recipientId, dispatch.NotificationId.Value, alertEvent, cancellationToken);
                    dispatch.MarkSignalRSent();
                }
            }

            await context.SaveChangesAsync(cancellationToken);

            var pushRecipientIds = alertEvent.SuppressPush
                ? new List<Guid>()
                : await ResolvePushRecipientIdsAsync(context, recipientIds, alertEvent.Category, cancellationToken);

            var pushResult = alertEvent.SuppressPush || pushRecipientIds.Count == 0
                ? new OneSignalPushDispatchResult(false, false, true, null, null, alertEvent.SuppressPush ? "Push suppressed." : "No admin web push recipients opted in.")
                : SummarizePushResults(await pushService.SendToExternalUsersAsync(
                    pushRecipientIds.Select(id => id.ToString()).ToArray(),
                    alertEvent.TitleAr,
                    alertEvent.TitleEn,
                    alertEvent.BodyAr,
                    alertEvent.BodyEn,
                    alertEvent.Type,
                    alertEvent.ReferenceId,
                    alertEvent.DataJson,
                    alertEvent.TargetUrl,
                    OneSignalPushProfile.Default,
                    OneSignalApplicationTarget.AdminWeb,
                    cancellationToken));

            foreach (var dispatch in alertEvent.Dispatches.Where(item => recipientIds.Contains(item.AdminUserId)))
            {
                var wasPushCandidate = pushRecipientIds.Contains(dispatch.AdminUserId);
                dispatch.MarkPushResult(
                    pushResult.Attempted && wasPushCandidate,
                    pushResult.Sent && wasPushCandidate,
                    !wasPushCandidate || pushResult.Skipped,
                    pushResult.Reason);
            }

            alertEvent.MarkCompleted();
            await context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Admin alert event {EventId} dispatched. Type: {Type}. Recipients: {RecipientCount}. SignalR: {SignalRCount}. PushAttempted: {PushAttempted}. PushSent: {PushSent}. PushSkipped: {PushSkipped}.",
                alertEvent.Id,
                alertEvent.Type,
                recipientIds.Count,
                alertEvent.Dispatches.Count(item => item.SignalRSent),
                pushResult.Attempted,
                pushResult.Sent,
                pushResult.Skipped);

            if (pushResult.Attempted && !pushResult.Sent && !alertEvent.SuppressPush)
            {
                _logger.LogWarning(
                    "Admin alert event {EventId} inbox/SignalR dispatch completed, but OneSignal push failed. Type: {Type}. StatusCode: {StatusCode}. Reason: {Reason}",
                    alertEvent.Id,
                    alertEvent.Type,
                    pushResult.ProviderStatusCode,
                    pushResult.Reason);
            }

            if (ShouldCreatePushFailureAlert() && pushResult.Attempted && !pushResult.Sent && !alertEvent.SuppressPush && alertEvent.Type != AdminAlertTypes.SystemOneSignalFailure)
            {
                await adminAlertService.SendAsync(
                    new AdminAlertRequest(
                        AdminAlertTypes.SystemOneSignalFailure,
                        AdminAlertCategories.System,
                        AdminAlertPriorities.High,
                        "فشل إرسال OneSignal للأدمن",
                        "Admin OneSignal dispatch failed",
                        "تم حفظ الإشعار داخل اللوحة لكن فشل إرسال Web Push لبعض أو كل الأدمنز.",
                        "The inbox notification was saved, but admin Web Push failed for some or all recipients.",
                        alertEvent.Id,
                        "/notifications",
                        new
                        {
                            sourceEventId = alertEvent.Id,
                            sourceType = alertEvent.Type,
                            reason = pushResult.Reason,
                            providerStatusCode = pushResult.ProviderStatusCode
                        },
                        SuppressPush: true),
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            var nextAttemptAt = DateTime.UtcNow.Add(ResolveRetryDelay(alertEvent.Attempts));
            alertEvent.MarkFailed(ex.Message, nextAttemptAt, MaxAttempts);
            await context.SaveChangesAsync(cancellationToken);

            _logger.LogError(
                ex,
                "Admin alert event {EventId} failed on attempt {Attempt}. Next status: {Status}.",
                alertEvent.Id,
                alertEvent.Attempts,
                alertEvent.Status);
        }
    }

    private async Task SendSignalRAsync(
        Guid recipientId,
        Guid notificationId,
        AdminAlertEvent alertEvent,
        CancellationToken cancellationToken)
    {
        var payload = new NotificationPayload(
            notificationId,
            alertEvent.TitleAr,
            alertEvent.TitleEn,
            alertEvent.BodyAr,
            alertEvent.BodyEn,
            alertEvent.Type,
            alertEvent.Category,
            alertEvent.Priority,
            alertEvent.ReferenceId,
            alertEvent.DataJson,
            TryParseJson(alertEvent.DataJson),
            false,
            DateTime.UtcNow);

        await _hubContext.Clients
            .Group(NotificationHub.GetUserGroup(recipientId))
            .SendAsync(NotificationHub.ReceiveNotificationMethod, payload, cancellationToken);
    }

    private static async Task<List<Guid>> ResolvePushRecipientIdsAsync(
        ApplicationDbContext context,
        IReadOnlyCollection<Guid> recipientIds,
        string category,
        CancellationToken cancellationToken)
    {
        var optedIn = await context.UserPushDevices
            .AsNoTracking()
            .Where(device =>
                recipientIds.Contains(device.UserId) &&
                device.Platform == PushPlatform.Web &&
                device.IsActive &&
                device.NotificationsEnabled)
            .ToListAsync(cancellationToken);

        return optedIn
            .Where(device => device.IsAdminPushAllowedForCategory(category))
            .Select(device => device.UserId)
            .Distinct()
            .ToList();
    }

    private static OneSignalPushDispatchResult SummarizePushResults(IReadOnlyList<OneSignalPushDispatchResult> results)
    {
        if (results.Count == 0)
        {
            return new OneSignalPushDispatchResult(false, false, true, null, null, "No OneSignal push batches were produced.");
        }

        return new OneSignalPushDispatchResult(
            results.Any(result => result.Attempted),
            results.Any(result => result.Sent),
            results.All(result => result.Skipped),
            results.FirstOrDefault(result => result.ProviderStatusCode.HasValue)?.ProviderStatusCode,
            results.FirstOrDefault(result => !string.IsNullOrWhiteSpace(result.ProviderNotificationId))?.ProviderNotificationId,
            results.FirstOrDefault(result => !string.IsNullOrWhiteSpace(result.Reason))?.Reason);
    }

    private static TimeSpan ResolveRetryDelay(int attempts)
    {
        var index = Math.Clamp(attempts - 1, 0, RetryDelays.Length - 1);
        return RetryDelays[index];
    }

    private static bool ShouldCreatePushFailureAlert() => true;

    private static JsonElement? TryParseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
        catch
        {
            return null;
        }
    }
}
