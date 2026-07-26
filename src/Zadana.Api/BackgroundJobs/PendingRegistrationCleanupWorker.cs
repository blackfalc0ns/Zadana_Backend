using Microsoft.EntityFrameworkCore;
using Zadana.Application.Modules.Identity.Interfaces;

namespace Zadana.Api.BackgroundJobs;

/// <summary>
/// Removes abandoned pending registrations after their TTL expires so emails/phones
/// can be reused without occupying AspNetUsers.
/// </summary>
public sealed class PendingRegistrationCleanupWorker : BackgroundService
{
    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PendingRegistrationCleanupWorker> _logger;

    public PendingRegistrationCleanupWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<PendingRegistrationCleanupWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PendingRegistrationCleanupWorker starting...");
        await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var pendingService = scope.ServiceProvider.GetRequiredService<IPendingRegistrationService>();
                var deleted = await pendingService.CleanupExpiredAsync(stoppingToken);
                if (deleted > 0)
                {
                    _logger.LogInformation(
                        "PendingRegistrationCleanupWorker removed {Count} expired pending registrations.",
                        deleted);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PendingRegistrationCleanupWorker encountered an error.");
            }

            await Task.Delay(RunInterval, stoppingToken);
        }
    }
}
