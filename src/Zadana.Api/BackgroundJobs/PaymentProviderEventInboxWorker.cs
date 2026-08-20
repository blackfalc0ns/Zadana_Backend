using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Payments.Commands.ConfirmCardPayment;
using Zadana.Domain.Modules.Payments.Entities;
using Zadana.Domain.Modules.Payments.Enums;

namespace Zadana.Api.BackgroundJobs;

/// <summary>
/// Processes durable payment-provider inbox rows after the webhook endpoint has
/// acknowledged receipt. This keeps Moyasar delivery fast and gives transient
/// confirmation failures a retry path.
/// </summary>
public class PaymentProviderEventInboxWorker : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan FailedRetryDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan ProcessingStaleAfter = TimeSpan.FromMinutes(5);
    private const int MaxAttempts = 8;
    private const int BatchSize = 20;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PaymentProviderEventInboxWorker> _logger;

    public PaymentProviderEventInboxWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<PaymentProviderEventInboxWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PaymentProviderEventInboxWorker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PaymentProviderEventInboxWorker encountered an error.");
            }

            await Task.Delay(TickInterval, stoppingToken);
        }

        _logger.LogInformation("PaymentProviderEventInboxWorker stopped.");
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var now = DateTime.UtcNow;
        var failedCutoff = now.Subtract(FailedRetryDelay);
        var processingCutoff = now.Subtract(ProcessingStaleAfter);

        var inboxItems = await context.PaymentProviderEvents
            .Where(item =>
                item.SecretValid &&
                item.ProviderPaymentId != null &&
                item.ProcessingAttempts < MaxAttempts &&
                (item.Status == PaymentProviderEventStatus.Received ||
                 (item.Status == PaymentProviderEventStatus.Failed &&
                  (item.ProcessedAtUtc == null || item.ProcessedAtUtc <= failedCutoff)) ||
                 (item.Status == PaymentProviderEventStatus.Processing &&
                  item.ProcessingStartedAtUtc != null &&
                  item.ProcessingStartedAtUtc <= processingCutoff)))
            .OrderBy(item => item.ReceivedAtUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var inbox in inboxItems)
        {
            inbox.MarkProcessing();
            await context.SaveChangesAsync(cancellationToken);

            try
            {
                await sender.Send(
                    new ConfirmCardPaymentCommand(
                        PaymentId: null,
                        ProviderPaymentId: inbox.ProviderPaymentId,
                        ProviderName: inbox.ProviderName,
                        CustomerDeviceId: null),
                    cancellationToken);

                inbox.MarkProcessed();
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                inbox.MarkFailed(ex.Message);
                await context.SaveChangesAsync(cancellationToken);

                _logger.LogWarning(
                    ex,
                    "Payment provider event {Provider}:{EventId} failed on attempt {Attempt}.",
                    inbox.ProviderName,
                    inbox.ProviderEventId,
                    inbox.ProcessingAttempts);

                if (inbox.ProcessingAttempts >= MaxAttempts)
                {
                    _logger.LogError(
                        ex,
                        "Payment provider event {Provider}:{EventId} exhausted {MaxAttempts} processing attempts and is poisoned.",
                        inbox.ProviderName,
                        inbox.ProviderEventId,
                        MaxAttempts);

                    await TrySendPoisonAdminAlertAsync(scope.ServiceProvider, inbox, ex.Message, cancellationToken);
                }
            }
        }
    }

    private async Task TrySendPoisonAdminAlertAsync(
        IServiceProvider serviceProvider,
        PaymentProviderEventInbox inbox,
        string? failureReason,
        CancellationToken cancellationToken)
    {
        var adminAlertService = serviceProvider.GetService<IAdminAlertService>();
        if (adminAlertService is null)
        {
            return;
        }

        try
        {
            await adminAlertService.SendAsync(
                new AdminAlertRequest(
                    AdminAlertTypes.SystemIntegrationFailure,
                    AdminAlertCategories.System,
                    AdminAlertPriorities.Critical,
                    "فشل معالجة حدث الدفع",
                    "Payment provider inbox event poisoned",
                    $"تعذرت معالجة حدث {inbox.ProviderName} ({inbox.ProviderEventId}) بعد {MaxAttempts} محاولات.",
                    $"Payment provider event {inbox.ProviderName} ({inbox.ProviderEventId}) failed after {MaxAttempts} attempts.",
                    inbox.Id,
                    "/finances",
                    new
                    {
                        integration = "PaymentProviderEventInbox",
                        inbox.ProviderName,
                        inbox.ProviderEventId,
                        inbox.ProviderPaymentId,
                        inbox.ProcessingAttempts,
                        failureReason,
                    }),
                cancellationToken);
        }
        catch (Exception alertException)
        {
            _logger.LogError(
                alertException,
                "Failed to dispatch poisoned payment inbox admin alert for {Provider}:{EventId}.",
                inbox.ProviderName,
                inbox.ProviderEventId);
        }
    }
}
