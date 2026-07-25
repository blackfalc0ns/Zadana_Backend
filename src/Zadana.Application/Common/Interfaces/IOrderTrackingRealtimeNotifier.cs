using Zadana.Domain.Modules.Orders.Enums;

namespace Zadana.Application.Common.Interfaces;

/// <summary>
/// Broadcasts real-time order tracking events to everyone subscribed to a specific order
/// (customer, vendor, driver, admin) on the dedicated OrderTrackingHub group.
/// </summary>
public interface IOrderTrackingRealtimeNotifier
{
    /// <summary>
    /// Broadcast a fresh driver location to anyone tracking the given order.
    /// </summary>
    Task BroadcastDriverLocationAsync(
        Guid orderId,
        Guid driverId,
        decimal latitude,
        decimal longitude,
        decimal? accuracyMeters,
        DateTime recordedAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcast an order status change to the order tracking group.
    /// </summary>
    Task BroadcastOrderStatusChangedAsync(
        Guid orderId,
        string orderNumber,
        Guid vendorId,
        Guid customerUserId,
        OrderStatus oldStatus,
        OrderStatus newStatus,
        string? actorRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcast a driver arrival state change (arrived-at-vendor / arrived-at-customer / etc.)
    /// to the order tracking group.
    /// </summary>
    Task BroadcastDriverArrivalStateAsync(
        Guid orderId,
        string orderNumber,
        string arrivalState,
        string driverName,
        string? actorRole,
        CancellationToken cancellationToken = default);
}
