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

        // A disabled gateway must stop *new* automatic submissions, but it
        // must not make transfers that were already submitted disappear from
        // the operational queue.  We still refresh when the provider is
        // available and, in every case, keep stale transfers visible through
        // the review alerts below.
        if (payoutOrchestrator.HasEnabledGateway &&
            await payoutOrchestrator.IsAutomaticProcessingEnabledAsync(cancellationToken))
        {
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
        }

        // Provider status refresh remains active in Manual mode. It only
        // reconciles transfers that were submitted before the switch and never
        // creates a new payout or retry.
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

        // A manual bank submission is deliberately non-cancellable. Surface a
        // durable reminder if it remains unconfirmed for a full day so an
        // omitted proof, a failed transfer, or a missing bank statement is
        // handled through reconciliation rather than silently remaining in a
        // processing state forever.
        var manualConfirmationCutoff = DateTime.UtcNow.AddHours(-24);
        var submittedManualPayouts = await context.PayoutExecutionReservations
            .AsNoTracking()
            .Where(item =>
                item.Mode == PayoutExecutionMode.Manual &&
                item.Status == PayoutExecutionReservationStatus.Submitted &&
                item.SubmittedAtUtc != null &&
                item.SubmittedAtUtc <= manualConfirmationCutoff)
            .Join(
                context.Payouts.AsNoTracking(),
                reservation => reservation.PayoutId,
                payout => payout.Id,
                (reservation, payout) => new
                {
                    payout.Id,
                    payout.SettlementId,
                    payout.Amount,
                    reservation.SubmittedAtUtc,
                    reservation.SubmissionReference
                })
            .OrderBy(item => item.SubmittedAtUtc)
            .Take(50)
            .ToListAsync(cancellationToken);

        foreach (var payout in submittedManualPayouts)
        {
            await adminAlertService.SendAsync(
                new AdminAlertRequest(
                    AdminAlertTypes.PayoutRequiresReview,
                    AdminAlertCategories.Settlements,
                    AdminAlertPriorities.High,
                    "Manual payout needs reconciliation",
                    "Manual payout needs reconciliation",
                    $"Manual bank transfer for payout {payout.Id} was submitted more than 24 hours ago and still needs proof and confirmation.",
                    $"Manual bank transfer for payout {payout.Id} was submitted more than 24 hours ago and still needs proof and confirmation.",
                    payout.Id,
                    "/finances/settlements",
                    new
                    {
                        payout.Id,
                        payout.SettlementId,
                        payout.Amount,
                        payout.SubmittedAtUtc,
                        payout.SubmissionReference,
                        workflow = "manual-bank-submission-awaiting-confirmation"
                    }),
                cancellationToken);
        }
    }
}
