using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Delivery.Enums;
using Microsoft.EntityFrameworkCore;

namespace Zadana.Api.BackgroundJobs;

public class DeliveryDispatchWorker : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(10);

    /// <summary>
    /// After this duration without an active offer, the dispatch cycle is reset
    /// (clears previous rejections/timeouts) and a fresh round of offers begins.
    /// </summary>
    private static readonly TimeSpan ResetCycleCooldown = TimeSpan.FromMinutes(3);

    /// <summary>
    /// After this duration without a driver, an admin alert is sent.
    /// </summary>
    private static readonly TimeSpan AdminAlertThreshold = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Minimum interval between admin alerts for the same order to avoid spam.
    /// </summary>
    private static readonly TimeSpan AdminAlertCooldown = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DeliveryDispatchWorker> _logger;

    /// <summary>
    /// Tracks when the last admin alert was sent per order to avoid spamming.
    /// </summary>
    private readonly Dictionary<Guid, DateTime> _lastAlertSentAtUtc = new();

    public DeliveryDispatchWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<DeliveryDispatchWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DeliveryDispatchWorker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var dispatchService = scope.ServiceProvider.GetRequiredService<IDeliveryDispatchService>();
                var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                var adminAlertService = scope.ServiceProvider.GetService<IAdminAlertService>();

                // 1. Process any expired offers (timeout → offer next driver).
                await dispatchService.ProcessExpiredOffersAsync(stoppingToken);

                // 2. Find ready/in-progress orders stuck with no active offer.
                //    Order by PlacedAtUtc ascending so older orders get priority.
                var stuckOrders = await context.Orders
                    .AsNoTracking()
                    .Where(order =>
                        order.Status == OrderStatus.ReadyForPickup ||
                        order.Status == OrderStatus.DriverAssignmentInProgress)
                    .OrderBy(order => order.PlacedAtUtc)
                    .Select(order => new { order.Id, order.OrderNumber, order.PlacedAtUtc, order.VendorId })
                    .ToListAsync(stoppingToken);

                var now = DateTime.UtcNow;

                foreach (var stuckOrder in stuckOrders)
                {
                    try
                    {
                        // Check if there's already an accepted/active assignment for this order.
                        var hasAcceptedAssignment = await context.DeliveryAssignments
                            .AnyAsync(a =>
                                a.OrderId == stuckOrder.Id &&
                                (a.Status == AssignmentStatus.Accepted ||
                                 a.Status == AssignmentStatus.ArrivedAtVendor ||
                                 a.Status == AssignmentStatus.PickedUp ||
                                 a.Status == AssignmentStatus.ArrivedAtCustomer),
                                stoppingToken);

                        if (hasAcceptedAssignment)
                        {
                            // Order has a driver — clean up alert tracking.
                            _lastAlertSentAtUtc.Remove(stuckOrder.Id);
                            continue;
                        }

                        // Check if there's already an active (non-expired) offer for this order.
                        var hasActiveOffer = await context.DeliveryAssignments
                            .AnyAsync(a =>
                                a.OrderId == stuckOrder.Id &&
                                a.Status == AssignmentStatus.OfferSent &&
                                a.OfferExpiresAtUtc.HasValue &&
                                a.OfferExpiresAtUtc.Value > now,
                                stoppingToken);

                        if (hasActiveOffer)
                        {
                            continue;
                        }

                        // Determine how long this order has been waiting for a driver.
                        var lastOfferActivity = await context.DeliveryOfferAttempts
                            .Where(a => a.OrderId == stuckOrder.Id)
                            .OrderByDescending(a => a.OfferedAtUtc)
                            .Select(a => a.OfferedAtUtc)
                            .FirstOrDefaultAsync(stoppingToken);

                        var waitingSince = lastOfferActivity != default
                            ? lastOfferActivity
                            : stuckOrder.PlacedAtUtc;

                        var waitingDuration = now - waitingSince;

                        // Decide whether to reset the dispatch cycle or just retry.
                        var shouldResetCycle = waitingDuration >= ResetCycleCooldown;

                        _logger.LogInformation(
                            "DeliveryDispatchWorker: retrying dispatch for order {OrderId} (waiting {WaitingMinutes:N1} min, resetCycle={ResetCycle}).",
                            stuckOrder.Id,
                            waitingDuration.TotalMinutes,
                            shouldResetCycle);

                        await dispatchService.TryAutoDispatchAsync(
                            stuckOrder.Id,
                            resetCycle: shouldResetCycle,
                            cancellationToken: stoppingToken);

                        // Send admin alert if order has been waiting too long.
                        if (adminAlertService is not null && waitingDuration >= AdminAlertThreshold)
                        {
                            await TrySendAdminAlertAsync(
                                adminAlertService,
                                stuckOrder.Id,
                                stuckOrder.OrderNumber,
                                waitingDuration,
                                now,
                                stoppingToken);
                        }
                    }
                    catch (Exception orderEx)
                    {
                        _logger.LogWarning(orderEx,
                            "DeliveryDispatchWorker: failed to process order {OrderId}, skipping.",
                            stuckOrder.Id);
                    }
                }

                // Cleanup stale alert tracking entries for orders no longer stuck.
                CleanupAlertTracking(stuckOrders.Select(o => o.Id).ToHashSet());
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeliveryDispatchWorker encountered an error.");
            }

            await Task.Delay(TickInterval, stoppingToken);
        }

        _logger.LogInformation("DeliveryDispatchWorker stopped.");
    }

    private async Task TrySendAdminAlertAsync(
        IAdminAlertService adminAlertService,
        Guid orderId,
        string orderNumber,
        TimeSpan waitingDuration,
        DateTime now,
        CancellationToken cancellationToken)
    {
        // Check cooldown to avoid spamming.
        if (_lastAlertSentAtUtc.TryGetValue(orderId, out var lastSent) &&
            (now - lastSent) < AdminAlertCooldown)
        {
            return;
        }

        try
        {
            var waitingMinutes = (int)waitingDuration.TotalMinutes;

            await adminAlertService.SendAsync(
                new AdminAlertRequest(
                    AdminAlertTypes.DeliveryDispatchStuck,
                    AdminAlertCategories.Delivery,
                    waitingMinutes >= 10 ? AdminAlertPriorities.Critical : AdminAlertPriorities.High,
                    $"طلب بدون مندوب منذ {waitingMinutes} دقيقة",
                    $"Order without driver for {waitingMinutes} minutes",
                    $"الطلب #{orderNumber} ينتظر مندوب توصيل منذ {waitingMinutes} دقيقة. يرجى التدخل يدوياً أو التأكد من توفر مندوبين.",
                    $"Order #{orderNumber} has been waiting for a delivery driver for {waitingMinutes} minutes. Please intervene manually or ensure drivers are available.",
                    orderId,
                    $"/admin/orders/{orderId}",
                    new
                    {
                        orderId,
                        orderNumber,
                        waitingMinutes,
                        waitingSince = now - waitingDuration
                    }),
                cancellationToken);

            _lastAlertSentAtUtc[orderId] = now;

            _logger.LogInformation(
                "DeliveryDispatchWorker: admin alert sent for order {OrderId} (waiting {WaitingMinutes} min).",
                orderId,
                waitingMinutes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "DeliveryDispatchWorker: failed to send admin alert for order {OrderId}.",
                orderId);
        }
    }

    private void CleanupAlertTracking(HashSet<Guid> currentStuckOrderIds)
    {
        var staleKeys = _lastAlertSentAtUtc.Keys
            .Where(key => !currentStuckOrderIds.Contains(key))
            .ToList();

        foreach (var key in staleKeys)
        {
            _lastAlertSentAtUtc.Remove(key);
        }
    }
}
