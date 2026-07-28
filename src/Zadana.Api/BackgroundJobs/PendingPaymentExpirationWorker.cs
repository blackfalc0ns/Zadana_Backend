using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Settings;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.EmailCenter;
using Zadana.Application.Modules.EmailCenter.DTOs;
using Zadana.Application.Modules.EmailCenter.Interfaces;
using Zadana.Application.Modules.Orders.Events;
using Zadana.Application.Modules.Orders.Services;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Entities;
using Zadana.Domain.Modules.Payments.Enums;

namespace Zadana.Api.BackgroundJobs;

/// <summary>
/// Cancels unpaid payment-reserved orders after their payment window expires.
/// Currency-agnostic; provider-agnostic.
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

    public Task RunOnceAsync(CancellationToken cancellationToken = default) =>
        ExpireStalePendingPaymentsAsync(cancellationToken);

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
        var inventoryWorkflowService = scope.ServiceProvider.GetService<OrderInventoryWorkflowService>()
            ?? new OrderInventoryWorkflowService(context);
        var publisher = scope.ServiceProvider.GetService<IPublisher>();

        var now = DateTime.UtcNow;
        var cardExpiration = ResolveCardExpiration();
        var bankTransferExpiration = ResolveBankTransferExpiration();

        var expiredCardOrders = await ExpireStaleCardPaymentOrdersAsync(
            context,
            inventoryWorkflowService,
            now,
            cardExpiration,
            cancellationToken);
        var expiredBankTransferOrders = await ExpireStaleBankTransferOrdersAsync(
            context,
            inventoryWorkflowService,
            now,
            bankTransferExpiration,
            cancellationToken);

        var expiredOrders = expiredCardOrders.Concat(expiredBankTransferOrders).ToList();
        if (expiredOrders.Count == 0)
        {
            return;
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogInformation(
                ex,
                "Skipped pending payment expiration batch because one or more orders/payments changed concurrently.");
            return;
        }

        foreach (var expiredOrder in expiredOrders)
        {
            await DispatchPaymentExpiredEmailAsync(
                context,
                emailCenterService,
                expiredOrder.OrderId,
                expiredOrder.CustomerUpdateMessage,
                cancellationToken);

            await PublishOrderCancelledAsync(publisher, expiredOrder, cancellationToken);
        }

        _logger.LogInformation(
            "Cancelled {CardCount} stale card payment orders older than {CardExpirationMinutes} minutes and {BankCount} stale bank transfer orders older than {BankExpirationMinutes} minutes.",
            expiredCardOrders.Count,
            cardExpiration.TotalMinutes,
            expiredBankTransferOrders.Count,
            bankTransferExpiration.TotalMinutes);
    }

    private async Task<List<ExpiredPaymentOrder>> ExpireStaleCardPaymentOrdersAsync(
        IApplicationDbContext context,
        OrderInventoryWorkflowService inventoryWorkflowService,
        DateTime now,
        TimeSpan expiration,
        CancellationToken cancellationToken)
    {
        var cutoff = now.Subtract(expiration);
        var candidatePayments = await context.Payments
            .Include(payment => payment.Order)
            .Where(payment =>
                (payment.Method == PaymentMethodType.Card
                 || payment.Method == PaymentMethodType.ApplePay
                 || payment.Method == PaymentMethodType.Mada) &&
                payment.CreatedAtUtc <= cutoff &&
                (payment.Order.PaymentMethod == PaymentMethodType.Card
                 || payment.Order.PaymentMethod == PaymentMethodType.ApplePay
                 || payment.Order.PaymentMethod == PaymentMethodType.Mada) &&
                payment.Order.Status == OrderStatus.PendingPayment &&
                payment.Order.PaymentStatus != PaymentStatus.Paid &&
                payment.Status != PaymentStatus.Paid)
            .OrderBy(payment => payment.CreatedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);

        var latestPayments = await LoadLatestPaymentsForCandidateOrdersAsync(
            context,
            candidatePayments,
            method => method.IsOnlineGatewayMethod(),
            cancellationToken);

        var expiredOrders = new List<ExpiredPaymentOrder>();
        foreach (var payment in latestPayments)
        {
            if (payment.CreatedAtUtc > cutoff ||
                payment.Status == PaymentStatus.Paid ||
                payment.Order.PaymentStatus == PaymentStatus.Paid ||
                payment.Order.Status != OrderStatus.PendingPayment)
            {
                continue;
            }

            if (payment.Status is PaymentStatus.Initiated or PaymentStatus.Pending)
            {
                payment.MarkAsFailed("Payment session expired before confirmation.");
            }

            var oldStatus = payment.Order.Status;
            payment.Order.ChangeStatus(OrderStatus.Cancelled, null, "Card payment reservation expired before confirmation.");
            context.OrderStatusHistories.Add(payment.Order.StatusHistory.Last());
            await inventoryWorkflowService.ApplyRestockAsync(payment.Order.Id, "card_payment_reservation_expired", cancellationToken);

            expiredOrders.Add(ToExpiredPaymentOrder(
                payment,
                oldStatus,
                $"Payment session expired for order {payment.Order.OrderNumber}. The order was cancelled and reserved items were released. Please place a new order to continue."));
        }

        return expiredOrders;
    }

    private async Task<List<ExpiredPaymentOrder>> ExpireStaleBankTransferOrdersAsync(
        IApplicationDbContext context,
        OrderInventoryWorkflowService inventoryWorkflowService,
        DateTime now,
        TimeSpan fallbackExpiration,
        CancellationToken cancellationToken)
    {
        var candidateCutoff = now.Subtract(MinimumExpiration);
        var candidatePayments = await context.Payments
            .Include(payment => payment.Order)
            .Where(payment =>
                payment.Method == PaymentMethodType.BankTransfer &&
                payment.CreatedAtUtc <= candidateCutoff &&
                payment.Order.PaymentMethod == PaymentMethodType.BankTransfer &&
                (payment.Order.Status == OrderStatus.PendingBankConfirmation || payment.Order.Status == OrderStatus.PendingPayment) &&
                payment.Order.PaymentStatus != PaymentStatus.Paid &&
                payment.Status != PaymentStatus.Paid)
            .OrderBy(payment => payment.CreatedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);

        var latestPayments = await LoadLatestPaymentsForCandidateOrdersAsync(
            context,
            candidatePayments,
            method => method == PaymentMethodType.BankTransfer,
            cancellationToken);

        var expiredOrders = new List<ExpiredPaymentOrder>();
        foreach (var payment in latestPayments)
        {
            if (payment.Status == PaymentStatus.Paid ||
                payment.Order.PaymentStatus == PaymentStatus.Paid ||
                payment.Order.Status is not (OrderStatus.PendingBankConfirmation or OrderStatus.PendingPayment) ||
                HasBankTransferProof(payment) ||
                ResolveBankTransferExpiresAtUtc(payment, fallbackExpiration) > now)
            {
                continue;
            }

            if (payment.Status is PaymentStatus.Initiated or PaymentStatus.Pending)
            {
                payment.MarkAsFailed("Bank transfer window expired before confirmation or proof upload.");
            }

            var oldStatus = payment.Order.Status;
            payment.Order.ChangeStatus(OrderStatus.Cancelled, null, "Bank transfer reservation expired before confirmation or proof upload.");
            context.OrderStatusHistories.Add(payment.Order.StatusHistory.Last());
            await inventoryWorkflowService.ApplyRestockAsync(payment.Order.Id, "bank_transfer_reservation_expired", cancellationToken);

            expiredOrders.Add(ToExpiredPaymentOrder(
                payment,
                oldStatus,
                $"Bank transfer window expired for order {payment.Order.OrderNumber}. The order was cancelled and reserved items were released. Please place a new order to continue."));
        }

        return expiredOrders;
    }

    private static async Task<List<Payment>> LoadLatestPaymentsForCandidateOrdersAsync(
        IApplicationDbContext context,
        IReadOnlyCollection<Payment> candidatePayments,
        Func<PaymentMethodType, bool> methodPredicate,
        CancellationToken cancellationToken)
    {
        var orderIds = candidatePayments
            .Select(payment => payment.OrderId)
            .Distinct()
            .ToArray();

        if (orderIds.Length == 0)
        {
            return [];
        }

        var payments = await context.Payments
            .Include(payment => payment.Order)
            .Where(payment => orderIds.Contains(payment.OrderId))
            .ToListAsync(cancellationToken);

        return payments
            .Where(payment => methodPredicate(payment.Method))
            .GroupBy(payment => payment.OrderId)
            .Select(group => group
                .OrderByDescending(payment => payment.CreatedAtUtc)
                .ThenByDescending(payment => payment.UpdatedAtUtc)
                .First())
            .ToList();
    }

    private TimeSpan ResolveCardExpiration()
    {
        var seconds = _configuration.GetValue<int?>("Payments:CardSessionExpirationSeconds") ?? 0;
        var configured = seconds > 0 ? TimeSpan.FromSeconds(seconds) : DefaultExpiration;
        return configured < MinimumExpiration ? MinimumExpiration : configured.Add(TimeSpan.FromMinutes(2));
    }

    private TimeSpan ResolveBankTransferExpiration()
    {
        var minutes = _configuration.GetValue<int?>($"{BankTransferSettingsOptions.SectionName}:ExpirationMinutes")
            ?? new BankTransferSettingsOptions().ExpirationMinutes;
        return TimeSpan.FromMinutes(Math.Max(minutes, (int)MinimumExpiration.TotalMinutes));
    }

    private static DateTime ResolveBankTransferExpiresAtUtc(Payment payment, TimeSpan fallbackExpiration)
    {
        if (!string.IsNullOrWhiteSpace(payment.RawFetchResponse))
        {
            try
            {
                using var document = System.Text.Json.JsonDocument.Parse(payment.RawFetchResponse);
                if (document.RootElement.TryGetProperty("expiresAtUtc", out var expiresAtElement) &&
                    expiresAtElement.ValueKind == System.Text.Json.JsonValueKind.String &&
                    expiresAtElement.TryGetDateTime(out var expiresAtUtc))
                {
                    return expiresAtUtc;
                }
            }
            catch (System.Text.Json.JsonException)
            {
                // Fall back to the configured window for older or malformed records.
            }
        }

        return payment.CreatedAtUtc.Add(fallbackExpiration);
    }

    private static bool HasBankTransferProof(Payment payment) =>
        string.Equals(payment.ProviderStatus, "proof_uploaded", StringComparison.OrdinalIgnoreCase);

    private static ExpiredPaymentOrder ToExpiredPaymentOrder(
        Payment payment,
        OrderStatus oldStatus,
        string customerUpdateMessage) =>
        new(
            payment.Order.Id,
            payment.Order.UserId,
            payment.Order.VendorId,
            payment.Order.OrderNumber,
            oldStatus,
            customerUpdateMessage);

    private async Task DispatchPaymentExpiredEmailAsync(
        IApplicationDbContext context,
        IEmailCenterService emailCenterService,
        Guid orderId,
        string updateMessage,
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
                    item.UserId,
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
                        ["update_message"] = updateMessage
                    },
                    TargetUrl: $"/orders/{emailData.Id}",
                    EntityId: emailData.Id,
                    RecipientEntityId: emailData.UserId,
                    VendorId: emailData.VendorId),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to dispatch pending payment expiration email for order {OrderId}.", orderId);
        }
    }

    private async Task PublishOrderCancelledAsync(
        IPublisher? publisher,
        ExpiredPaymentOrder expiredOrder,
        CancellationToken cancellationToken)
    {
        if (publisher is null)
        {
            return;
        }

        try
        {
            await publisher.Publish(
                new OrderStatusChangedNotification(
                    expiredOrder.OrderId,
                    expiredOrder.UserId,
                    expiredOrder.VendorId,
                    expiredOrder.OrderNumber,
                    expiredOrder.OldStatus,
                    OrderStatus.Cancelled,
                    NotifyCustomer: true,
                    NotifyVendor: false,
                    ActorRole: "payment_expiration_worker"),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish payment expiration cancellation for order {OrderId}.", expiredOrder.OrderId);
        }
    }

    private sealed record ExpiredPaymentOrder(
        Guid OrderId,
        Guid UserId,
        Guid VendorId,
        string OrderNumber,
        OrderStatus OldStatus,
        string CustomerUpdateMessage);
}
