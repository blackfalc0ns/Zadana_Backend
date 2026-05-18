using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;

namespace Zadana.Api.BackgroundJobs;

/// <summary>
/// Marks card payments as failed if the customer never completes the gateway
/// flow within the configured window. Currency-agnostic; provider-agnostic.
/// </summary>
public class PendingPaymentExpirationWorker : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan MinimumExpiration = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PendingPaymentExpirationWorker> _logger;
    private readonly IConfiguration _configuration;

    public PendingPaymentExpirationWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<PendingPaymentExpirationWorker> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PendingPaymentExpirationWorker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExpireStalePendingPaymentsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PendingPaymentExpirationWorker encountered an error.");
            }

            await Task.Delay(TickInterval, stoppingToken);
        }

        _logger.LogInformation("PendingPaymentExpirationWorker stopped.");
    }

    private async Task ExpireStalePendingPaymentsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var expiration = ResolveExpiration();
        var cutoff = DateTime.UtcNow.Subtract(expiration);

        var stalePayments = await context.Payments
            .Include(payment => payment.Order)
            .Where(payment =>
                payment.Method == PaymentMethodType.Card &&
                (payment.Status == PaymentStatus.Initiated || payment.Status == PaymentStatus.Pending) &&
                payment.CreatedAtUtc <= cutoff &&
                payment.Order.Status == OrderStatus.PendingPayment &&
                payment.Order.PaymentStatus != PaymentStatus.Paid)
            .OrderBy(payment => payment.CreatedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);

        if (stalePayments.Count == 0)
        {
            return;
        }

        foreach (var payment in stalePayments)
        {
            payment.MarkAsFailed("Payment session expired before confirmation.");
        }

        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Expired {Count} stale pending card payments older than {ExpirationMinutes} minutes.",
            stalePayments.Count,
            expiration.TotalMinutes);
    }

    private TimeSpan ResolveExpiration()
    {
        var seconds = _configuration.GetValue<int?>("Payments:CardSessionExpirationSeconds") ?? 0;
        var configured = seconds > 0 ? TimeSpan.FromSeconds(seconds) : DefaultExpiration;
        return configured < MinimumExpiration ? MinimumExpiration : configured.Add(TimeSpan.FromMinutes(2));
    }
}
