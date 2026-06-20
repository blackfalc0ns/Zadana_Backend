using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Finances.Services;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.Infrastructure.Settings;

namespace Zadana.Api.BackgroundJobs;

public sealed class PayoutStatusSyncWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PayoutStatusSyncWorker> _logger;
    private readonly MoyasarSettings _settings;

    public PayoutStatusSyncWorker(
        IServiceProvider serviceProvider,
        ILogger<PayoutStatusSyncWorker> logger,
        IOptions<MoyasarSettings> settings)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _settings = settings.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PayoutStatusSyncWorker encountered an error.");
            }

            var interval = Math.Max(_settings.Payouts.PollingIntervalSeconds, 60);
            await Task.Delay(TimeSpan.FromSeconds(interval), stoppingToken);
        }
    }

    private async Task SyncAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var payoutOrchestrator = scope.ServiceProvider.GetRequiredService<PayoutOrchestrator>();
        var adminAlertService = scope.ServiceProvider.GetRequiredService<IAdminAlertService>();

        if (!payoutOrchestrator.HasEnabledGateway)
        {
            return;
        }

        var pendingPayoutIds = await context.Payouts
            .AsNoTracking()
            .Where(item => item.Status == PayoutStatus.Pending && item.DestinationType != PayoutDestinationType.Manual)
            .OrderBy(item => item.CreatedAtUtc)
            .Select(item => item.Id)
            .Take(50)
            .ToListAsync(cancellationToken);

        foreach (var payoutId in pendingPayoutIds)
        {
            try
            {
                await payoutOrchestrator.TriggerAsync(payoutId, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to trigger pending payout {PayoutId}", payoutId);
            }
        }

        var unknownRetryCutoff = DateTime.UtcNow.AddSeconds(-Math.Max(_settings.Payouts.UnknownRetryDelaySeconds, 60));
        var unknownPayoutIds = await context.Payouts
            .AsNoTracking()
            .Where(item =>
                (item.Status == PayoutStatus.Queued || item.Status == PayoutStatus.Processing) &&
                item.ProviderTransferId == null &&
                item.ProviderName != "Manual" &&
                item.TriggeredAtUtc != null &&
                item.TriggeredAtUtc <= unknownRetryCutoff)
            .OrderBy(item => item.TriggeredAtUtc ?? item.CreatedAtUtc)
            .Select(item => item.Id)
            .Take(25)
            .ToListAsync(cancellationToken);

        foreach (var payoutId in unknownPayoutIds)
        {
            try
            {
                await payoutOrchestrator.TriggerAsync(payoutId, isRetry: true, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retry unknown payout {PayoutId}", payoutId);
            }
        }

        var activePayoutIds = await context.Payouts
            .AsNoTracking()
            .Where(item =>
                (item.Status == PayoutStatus.Queued || item.Status == PayoutStatus.Processing) &&
                item.ProviderTransferId != null &&
                item.ProviderName != "Manual")
            .OrderBy(item => item.TriggeredAtUtc ?? item.CreatedAtUtc)
            .Select(item => item.Id)
            .Take(100)
            .ToListAsync(cancellationToken);

        foreach (var payoutId in activePayoutIds)
        {
            try
            {
                await payoutOrchestrator.RefreshStatusAsync(payoutId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh payout {PayoutId}", payoutId);
            }
        }

        var staleCutoff = DateTime.UtcNow.AddMinutes(-Math.Max(_settings.Payouts.ProcessingAlertAfterMinutes, 10));
        var stalePayouts = await context.Payouts
            .AsNoTracking()
            .Where(item =>
                (item.Status == PayoutStatus.Queued || item.Status == PayoutStatus.Processing) &&
                item.ProviderName != "Manual" &&
                item.TriggeredAtUtc != null &&
                item.TriggeredAtUtc <= staleCutoff)
            .OrderBy(item => item.TriggeredAtUtc ?? item.CreatedAtUtc)
            .Select(item => new
            {
                item.Id,
                item.SettlementId,
                item.Amount,
                item.Status,
                item.ProviderName,
                item.ProviderTransferId,
                item.ProviderSequenceNumber,
                item.TriggeredAtUtc
            })
            .Take(25)
            .ToListAsync(cancellationToken);

        foreach (var payout in stalePayouts)
        {
            await adminAlertService.SendAsync(
                new AdminAlertRequest(
                    AdminAlertTypes.PayoutRequiresReview,
                    AdminAlertCategories.Settlements,
                    AdminAlertPriorities.High,
                    "Payout still processing",
                    "Payout still processing",
                    $"Payout {payout.Id} is still {payout.Status} after the configured payout processing threshold.",
                    $"Payout {payout.Id} is still {payout.Status} after the configured payout processing threshold.",
                    payout.Id,
                    "/finances/withdrawals",
                    new
                    {
                        payout.Id,
                        payout.SettlementId,
                        payout.Amount,
                        status = payout.Status.ToString(),
                        payout.ProviderName,
                        payout.ProviderTransferId,
                        payout.ProviderSequenceNumber,
                        payout.TriggeredAtUtc
                    }),
                cancellationToken);
        }
    }
}
