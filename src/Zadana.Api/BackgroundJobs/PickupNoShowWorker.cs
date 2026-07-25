using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Orders.Events;
using Zadana.Application.Modules.Orders.Services;
using Zadana.Application.Modules.Orders.Support;
using Zadana.Application.Modules.Payments.Interfaces;
using Zadana.Application.Modules.Vendors.Support;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Social.Enums;

namespace Zadana.Api.BackgroundJobs;

public class PickupNoShowWorker : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PickupNoShowWorker> _logger;

    public PickupNoShowWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<PickupNoShowWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PickupNoShowWorker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPickupOrdersAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                SafeLogError(ex, "PickupNoShowWorker encountered an error.");
            }

            await Task.Delay(TickInterval, stoppingToken);
        }

        SafeLogInformation("PickupNoShowWorker stopped.");
    }

    private async Task ProcessPickupOrdersAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var gatewayResolver = scope.ServiceProvider.GetService<IPaymentGatewayResolver>();
        var inventoryWorkflowService = scope.ServiceProvider.GetService<OrderInventoryWorkflowService>()
            ?? new OrderInventoryWorkflowService(context);

        var settings = await PlatformPickupSettingsSupport.LoadAsync(context, cancellationToken);
        var noShowTimeout = PlatformPickupSettingsSupport.ResolveNoShowTimeout(settings);
        var now = DateTime.UtcNow;

        var candidateOrders = await context.Orders
            .Include(order => order.StatusHistory)
            .Where(order =>
                order.Fulfillment == FulfillmentType.Pickup &&
                order.Status == OrderStatus.ReadyForPickup)
            .OrderBy(order => order.ReadyForPickupAtUtc ?? order.PlacedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);

        if (candidateOrders.Count == 0)
        {
            return;
        }

        var branchIds = candidateOrders
            .Where(order => order.VendorBranchId.HasValue)
            .Select(order => order.VendorBranchId!.Value)
            .Distinct()
            .ToArray();

        var operatingHours = branchIds.Length == 0
            ? []
            : await context.BranchOperatingHours
                .AsNoTracking()
                .Where(hour => branchIds.Contains(hour.BranchId))
                .ToListAsync(cancellationToken);

        var hoursByBranchId = operatingHours
            .GroupBy(hour => hour.BranchId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<Domain.Modules.Vendors.Entities.BranchOperatingHour>)group.ToList());

        var publishedNotifications = new List<OrderStatusChangedNotification>();
        var processedCount = 0;

        foreach (var order in candidateOrders)
        {
            try
            {
                var deadline = order.PickupNoShowDeadlineUtc
                    ?? order.ReadyForPickupAtUtc?.Add(noShowTimeout)
                    ?? order.PlacedAtUtc.Add(noShowTimeout);

                var branchHours = order.VendorBranchId.HasValue &&
                                  hoursByBranchId.TryGetValue(order.VendorBranchId.Value, out var hours)
                    ? hours
                    : [];

                if (now < deadline)
                {
                    await TrySendPickupRemindersAsync(
                        order,
                        deadline,
                        notificationService,
                        unitOfWork,
                        cancellationToken);
                    continue;
                }

                if (!BranchOperatingHoursSupport.IsBranchOpenAt(branchHours, now))
                {
                    var extendedDeadline = BranchOperatingHoursSupport.ResolveExtendedDeadlineUtc(branchHours, now);
                    if (!order.PickupNoShowDeadlineUtc.HasValue || extendedDeadline > order.PickupNoShowDeadlineUtc.Value)
                    {
                        order.ExtendPickupNoShowDeadline(extendedDeadline);
                        await unitOfWork.SaveChangesAsync(cancellationToken);
                        await SendDeadlineExtendedNotificationAsync(order, notificationService, cancellationToken);
                    }

                    continue;
                }

                var oldStatus = order.Status;
                order.ChangeStatus(OrderStatus.Cancelled, null, "PickupExpired");
                context.OrderStatusHistories.Add(order.StatusHistory.Last());
                await inventoryWorkflowService.ApplyRestockAsync(order.Id, "pickup_expired", cancellationToken);

                await OrderCancellationRefundSupport.TryRefundPaidOrderAsync(
                    context,
                    gatewayResolver,
                    _logger,
                    order,
                    "Pickup no-show expired",
                    cancellationToken);

                await SendPickupExpiredNotificationsAsync(order, notificationService, context, cancellationToken);

                publishedNotifications.Add(new OrderStatusChangedNotification(
                    order.Id,
                    order.UserId,
                    order.VendorId,
                    order.OrderNumber,
                    oldStatus,
                    OrderStatus.Cancelled,
                    NotifyCustomer: false,
                    NotifyVendor: false,
                    ActorRole: "pickup_no_show_worker"));

                processedCount++;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                SafeLogWarning(
                    ex,
                    "PickupNoShowWorker: order {OrderId} changed concurrently, skipping.",
                    order.Id);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                SafeLogWarning(ex, "PickupNoShowWorker: failed to process order {OrderId}, skipping.", order.Id);
            }
        }

        if (processedCount == 0)
        {
            return;
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            SafeLogInformation(ex, "PickupNoShowWorker: batch save skipped due to concurrent updates.");
            return;
        }

        foreach (var notification in publishedNotifications)
        {
            try
            {
                await publisher.Publish(notification, cancellationToken);
            }
            catch (Exception ex)
            {
                SafeLogWarning(ex, "PickupNoShowWorker: failed to publish cancellation for order {OrderId}.", notification.OrderId);
            }
        }

        SafeLogInformation("PickupNoShowWorker cancelled {Count} expired pickup orders.", processedCount);
    }

    private static async Task TrySendPickupRemindersAsync(
        Domain.Modules.Orders.Entities.Order order,
        DateTime deadline,
        INotificationService notificationService,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var windowStart = order.ReadyForPickupAtUtc ?? order.PlacedAtUtc;
        if (deadline <= windowStart)
        {
            return;
        }

        var window = deadline - windowStart;
        var now = DateTime.UtcNow;
        var midpoint = windowStart.Add(TimeSpan.FromTicks(window.Ticks / 2));
        var nearDeadline = windowStart.Add(TimeSpan.FromTicks(window.Ticks * 9 / 10));

        if (!order.PickupReminder50Sent && now >= midpoint)
        {
            order.MarkPickupReminder50Sent();
            await SendPickupReminderNotificationAsync(order, notificationService, 50, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        if (!order.PickupReminder90Sent && now >= nearDeadline)
        {
            order.MarkPickupReminder90Sent();
            await SendPickupReminderNotificationAsync(order, notificationService, 90, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task SendPickupReminderNotificationAsync(
        Domain.Modules.Orders.Entities.Order order,
        INotificationService notificationService,
        int percent,
        CancellationToken cancellationToken)
    {
        await notificationService.SendToUserAsync(
            order.UserId,
            $"تذكير: {percent}% من مهلة الاستلام",
            $"Pickup reminder: {percent}% of pickup window elapsed",
            $"طلبك رقم {order.OrderNumber} ما زال جاهزًا للاستلام. تبقى {100 - percent}% من مهلة الاستلام.",
            $"Order #{order.OrderNumber} is still ready for pickup. {100 - percent}% of the pickup window remains.",
            NotificationTypes.PickupReminder,
            order.Id,
            $"orderId={order.Id};reminderPercent={percent}",
            cancellationToken);

        // TODO(pickup-sms): Send pickup reminder SMS to customer when SMS provider is integrated.
    }

    private static async Task SendDeadlineExtendedNotificationAsync(
        Domain.Modules.Orders.Entities.Order order,
        INotificationService notificationService,
        CancellationToken cancellationToken)
    {
        await notificationService.SendToUserAsync(
            order.UserId,
            "تم تمديد مهلة الاستلام",
            "Pickup deadline extended",
            $"مدّدنا مهلة استلام طلبك رقم {order.OrderNumber} لأن الفرع كان مغلقًا.",
            $"We extended the pickup deadline for order #{order.OrderNumber} because the branch was closed.",
            NotificationTypes.PickupDeadlineExtended,
            order.Id,
            $"orderId={order.Id}",
            cancellationToken);

        // TODO(pickup-sms): Send pickup deadline extension SMS when SMS provider is integrated.
    }

    private static async Task SendPickupExpiredNotificationsAsync(
        Domain.Modules.Orders.Entities.Order order,
        INotificationService notificationService,
        IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        await notificationService.SendToUserAsync(
            order.UserId,
            "انتهت مهلة الاستلام",
            "Pickup window expired",
            $"انتهت مهلة استلام طلبك رقم {order.OrderNumber} وأُلغي الطلب.",
            $"The pickup window for order #{order.OrderNumber} expired and the order was cancelled.",
            NotificationTypes.PickupExpired,
            order.Id,
            $"orderId={order.Id}",
            cancellationToken);

        var vendorUserId = await context.Vendors
            .AsNoTracking()
            .Where(vendor => vendor.Id == order.VendorId)
            .Select(vendor => vendor.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (vendorUserId != Guid.Empty)
        {
            await notificationService.SendToUserAsync(
                vendorUserId,
                "انتهت مهلة استلام طلب",
                "Pickup window expired",
                $"انتهت مهلة استلام الطلب رقم {order.OrderNumber} بدون حضور العميل.",
                $"Pickup window expired for order #{order.OrderNumber} without customer arrival.",
                NotificationTypes.PickupExpired,
                order.Id,
                $"orderId={order.Id}",
                cancellationToken);
        }

        // TODO(pickup-sms): Send pickup expired SMS to customer when SMS provider is integrated.
    }

    private void SafeLogWarning(Exception? ex, string message, params object[] args)
    {
        try { _logger.LogWarning(ex, message, args); }
        catch { }
    }

    private void SafeLogError(Exception? ex, string message, params object[] args)
    {
        try { _logger.LogError(ex, message, args); }
        catch { }
    }

    private void SafeLogInformation(string message, params object[] args)
    {
        try { _logger.LogInformation(message, args); }
        catch { }
    }

    private void SafeLogInformation(Exception? ex, string message, params object[] args)
    {
        try { _logger.LogInformation(ex, message, args); }
        catch { }
    }
}
