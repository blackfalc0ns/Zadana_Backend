using Microsoft.EntityFrameworkCore;
using Zadana.Infrastructure.Persistence;

namespace Zadana.Api.BackgroundJobs;

/// <summary>
/// Periodically removes old read notifications and completed admin alert events
/// to prevent unbounded table growth.
/// </summary>
public sealed class NotificationCleanupWorker : BackgroundService
{
    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(6);
    private const int NotificationRetentionDays = 90;
    private const int AdminAlertEventRetentionDays = 60;
    private const int BatchSize = 500;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationCleanupWorker> _logger;

    public NotificationCleanupWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationCleanupWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NotificationCleanupWorker starting...");

        // Initial delay to avoid startup contention
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NotificationCleanupWorker encountered an error.");
            }

            await Task.Delay(RunInterval, stoppingToken);
        }
    }

    private async Task RunCleanupAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var notificationCutoff = DateTime.UtcNow.AddDays(-NotificationRetentionDays);
        var alertEventCutoff = DateTime.UtcNow.AddDays(-AdminAlertEventRetentionDays);

        // Delete old READ notifications in batches
        var totalNotificationsDeleted = 0;
        int deleted;
        do
        {
            deleted = await context.Notifications
                .Where(n => n.IsRead && n.CreatedAtUtc < notificationCutoff)
                .Take(BatchSize)
                .ExecuteDeleteAsync(cancellationToken);

            totalNotificationsDeleted += deleted;
        } while (deleted == BatchSize && !cancellationToken.IsCancellationRequested);

        // Delete old completed admin alert events in batches
        var totalAlertEventsDeleted = 0;
        do
        {
            deleted = await context.AdminAlertEvents
                .Where(e =>
                    (e.Status == Domain.Modules.Social.Enums.AdminAlertEventStatus.Completed ||
                     e.Status == Domain.Modules.Social.Enums.AdminAlertEventStatus.DeadLetter) &&
                    e.CreatedAtUtc < alertEventCutoff)
                .Take(BatchSize)
                .ExecuteDeleteAsync(cancellationToken);

            totalAlertEventsDeleted += deleted;
        } while (deleted == BatchSize && !cancellationToken.IsCancellationRequested);

        if (totalNotificationsDeleted > 0 || totalAlertEventsDeleted > 0)
        {
            _logger.LogInformation(
                "NotificationCleanupWorker completed. Notifications deleted: {NotificationsDeleted}. Alert events deleted: {AlertEventsDeleted}.",
                totalNotificationsDeleted,
                totalAlertEventsDeleted);
        }
    }
}
