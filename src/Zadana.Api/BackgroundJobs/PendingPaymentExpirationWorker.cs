using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.EmailCenter;
using Zadana.Application.Modules.EmailCenter.DTOs;
using Zadana.Application.Modules.EmailCenter.Interfaces;
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
        var emailCenterService = scope.ServiceProvider.GetRequiredService<IEmailCenterService>();
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

        foreach (var payment in stalePayments)
        {
            await DispatchPaymentExpiredEmailAsync(context, emailCenterService, payment.Order.Id, cancellationToken);
        }

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

    private async Task DispatchPaymentExpiredEmailAsync(
        IApplicationDbContext context,
        IEmailCenterService emailCenterService,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        try
        {
            var emailData = await context.Orders
                .AsNoTracking()
                .Where(item => item.Id == orderId)
                .Select(item => new
                {
                    item.Id,
                    item.OrderNumber,
                    item.VendorId,
                    CustomerName = item.User.FullName,
                    CustomerEmail = item.User.Email,
                    VendorName = string.IsNullOrWhiteSpace(item.Vendor.BusinessNameEn)
                        ? item.Vendor.BusinessNameAr
                        : item.Vendor.BusinessNameEn
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (emailData is null)
            {
                return;
            }

            await emailCenterService.DispatchSystemEventEmailAsync(
                new EmailSystemEventDispatchRequest(
                    EventKey: EmailEventKeys.CustomerOrderImportantUpdate,
                    AudienceType: "customers",
                    To: string.IsNullOrWhiteSpace(emailData.CustomerEmail) ? [] : [emailData.CustomerEmail],
                    Variables: new Dictionary<string, string>
                    {
                        ["customer_name"] = string.IsNullOrWhiteSpace(emailData.CustomerName) ? "Customer" : emailData.CustomerName,
                        ["order_number"] = emailData.OrderNumber,
                        ["vendor_name"] = emailData.VendorName,
                        ["update_message"] = $"Payment session expired for order {emailData.OrderNumber}. Please retry payment from the app."
                    },
                    TargetUrl: $"/orders/{emailData.Id}",
                    EntityId: emailData.Id,
                    VendorId: emailData.VendorId),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to dispatch pending payment expiration email for order {OrderId}.", orderId);
        }
    }
}
