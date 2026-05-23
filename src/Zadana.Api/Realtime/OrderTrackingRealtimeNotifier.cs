using Microsoft.AspNetCore.SignalR;
using Zadana.Api.Realtime.Contracts;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Orders.Enums;

namespace Zadana.Api.Realtime;

public sealed class OrderTrackingRealtimeNotifier : IOrderTrackingRealtimeNotifier
{
    private readonly IHubContext<OrderTrackingHub> _hubContext;
    private readonly ILogger<OrderTrackingRealtimeNotifier> _logger;

    public OrderTrackingRealtimeNotifier(
        IHubContext<OrderTrackingHub> hubContext,
        ILogger<OrderTrackingRealtimeNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task BroadcastDriverLocationAsync(
        Guid orderId,
        Guid driverId,
        decimal latitude,
        decimal longitude,
        decimal? accuracyMeters,
        DateTime recordedAtUtc,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new OrderTrackingDriverLocationPayload(
                orderId,
                driverId,
                latitude,
                longitude,
                accuracyMeters,
                recordedAtUtc);

            var groupName = OrderTrackingHub.GetOrderGroup(orderId);
            await _hubContext.Clients
                .Group(groupName)
                .SendAsync(OrderTrackingHub.ReceiveDriverLocationMethod, payload, cancellationToken);

            _logger.LogInformation(
                "[OrderTrackingHub] Sent ReceiveDriverLocation to group {Group} (orderId={OrderId}, driverId={DriverId}).",
                groupName, orderId, driverId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to broadcast driver location for order {OrderId} (driver {DriverId})",
                orderId,
                driverId);
        }
    }

    public async Task BroadcastOrderStatusChangedAsync(
        Guid orderId,
        string orderNumber,
        Guid vendorId,
        OrderStatus oldStatus,
        OrderStatus newStatus,
        string? actorRole,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var action = ResolveAction(newStatus);
            var targetUrl = $"/orders/{orderId}";

            var payload = new OrderStatusChangedRealtimePayload(
                orderId,
                orderNumber,
                vendorId,
                oldStatus.ToString(),
                newStatus.ToString(),
                actorRole,
                action,
                targetUrl,
                DateTime.UtcNow);

            await _hubContext.Clients
                .Group(OrderTrackingHub.GetOrderGroup(orderId))
                .SendAsync(OrderTrackingHub.ReceiveOrderStatusChangedMethod, payload, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to broadcast order tracking status change for order {OrderId}",
                orderId);
        }
    }

    public async Task BroadcastDriverArrivalStateAsync(
        Guid orderId,
        string orderNumber,
        string arrivalState,
        string driverName,
        string? actorRole,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var targetUrl = $"/orders/{orderId}";

            var payload = new DriverArrivalStateChangedRealtimePayload(
                orderId,
                orderNumber,
                arrivalState,
                driverName,
                actorRole,
                targetUrl,
                DateTime.UtcNow);

            await _hubContext.Clients
                .Group(OrderTrackingHub.GetOrderGroup(orderId))
                .SendAsync(OrderTrackingHub.ReceiveDriverArrivalStateChangedMethod, payload, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to broadcast driver arrival state for order {OrderId}",
                orderId);
        }
    }

    private static string ResolveAction(OrderStatus status) =>
        status switch
        {
            OrderStatus.Cancelled => "cancelled",
            OrderStatus.Refunded => "refunded",
            _ => "status_changed"
        };
}
