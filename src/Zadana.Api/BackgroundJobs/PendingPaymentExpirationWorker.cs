using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Infrastructure.Settings;

namespace Zadana.Api.BackgroundJobs;

public class PendingPaymentExpirationWorker : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan MinimumExpiration = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PendingPaymentExpirationWorker> _logger;

    public PendingPaymentExpirationWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<PendingPaymentExpirationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
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
        var settings = scope.ServiceProvider.GetRequiredService<IOptions<PaymobSettings>>().Value;
        var expiration = ResolveExpiration(settings);
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
            payment.MarkAsFailed("Paymob payment session expired before confirmation.");
        }

        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Expired {Count} stale pending Paymob payments older than {ExpirationMinutes} minutes.",
            stalePayments.Count,
            expiration.TotalMinutes);
    }

    private static TimeSpan ResolveExpiration(PaymobSettings settings)
    {
        var configured = settings.PaymentKeyExpirationSeconds > 0
            ? TimeSpan.FromSeconds(settings.PaymentKeyExpirationSeconds)
            : TimeSpan.FromMinutes(30);

        return configured < MinimumExpiration ? MinimumExpiration : configured.Add(TimeSpan.FromMinutes(2));
    }
}
