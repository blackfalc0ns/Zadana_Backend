using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Orders.Enums;
using Microsoft.EntityFrameworkCore;

namespace Zadana.Api.BackgroundJobs;

public class DeliveryDispatchWorker : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(10);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DeliveryDispatchWorker> _logger;

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

                // 1. Process any expired offers (timeout → offer next driver).
                await dispatchService.ProcessExpiredOffersAsync(stoppingToken);

                // 2. Find ready/in-progress orders stuck with no active offer.
                var stuckOrderIds = await context.Orders
                    .AsNoTracking()
                    .Where(order =>
                        order.Status == OrderStatus.ReadyForPickup ||
                        order.Status == OrderStatus.DriverAssignmentInProgress)
                    .Select(order => new { order.Id, order.CreatedAtUtc })
                    .ToListAsync(stoppingToken);

                foreach (var stuckOrder in stuckOrderIds)
                {
                    try
                    {
                        // Check if there's already an active (non-expired) offer for this order.
                        var hasActiveOffer = await context.DeliveryAssignments
                            .AnyAsync(a =>
                                a.OrderId == stuckOrder.Id &&
                                a.Status == Domain.Modules.Delivery.Enums.AssignmentStatus.OfferSent &&
                                a.OfferExpiresAtUtc.HasValue &&
                                a.OfferExpiresAtUtc.Value > DateTime.UtcNow,
                                stoppingToken);

                        if (!hasActiveOffer)
                        {
                            _logger.LogInformation(
                                "DeliveryDispatchWorker: retrying dispatch for stuck order {OrderId}.",
                                stuckOrder.Id);

                            await dispatchService.TryAutoDispatchAsync(stuckOrder.Id, cancellationToken: stoppingToken);
                        }
                    }
                    catch (Exception orderEx)
                    {
                        _logger.LogWarning(orderEx,
                            "DeliveryDispatchWorker: failed to process order {OrderId}, skipping.",
                            stuckOrder.Id);
                    }
                }
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
}
