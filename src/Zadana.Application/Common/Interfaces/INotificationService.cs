namespace Zadana.Application.Common.Interfaces;

public interface INotificationService
{
    /// <summary>
    /// Persist a notification to the user's inbox without sending any real-time SignalR events.
    /// </summary>
    Task PersistToUserAsync(
        Guid userId,
        string titleAr,
        string titleEn,
        string bodyAr,
        string bodyEn,
        string? type = null,
        Guid? referenceId = null,
        string? data = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send a notification to a specific user and persist it in the database.
    /// </summary>
    Task SendToUserAsync(
        Guid userId,
        string titleAr,
        string titleEn,
        string bodyAr,
        string bodyEn,
        string? type = null,
        Guid? referenceId = null,
        string? data = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send a real-time order status update to a specific user without requiring the client to parse inbox notifications first.
    /// </summary>
    Task SendOrderStatusChangedToUserAsync(
        Guid userId,
        Guid orderId,
        string orderNumber,
        Guid vendorId,
        string oldStatus,
        string newStatus,
        string? actorRole = null,
        string? action = null,
        string? targetUrl = null,
        CancellationToken cancellationToken = default);

    Task SendDriverArrivalStateChangedToUserAsync(
        Guid userId,
        Guid orderId,
        string orderNumber,
        string arrivalState,
        string driverName,
        string? actorRole = null,
        string? targetUrl = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send a real-time delivery offer event to a driver via SignalR so the mobile app
    /// can instantly display the offer countdown UI.
    /// </summary>
    Task SendDeliveryOfferToDriverAsync(
        Guid driverUserId,
        Guid assignmentId,
        Guid orderId,
        string orderNumber,
        string vendorName,
        decimal deliveryFee,
        int countdownSeconds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcast a notification to all connected customers via SignalR (real-time only, not persisted).
    /// </summary>
    Task BroadcastToAllCustomersAsync(
        string titleAr,
        string titleEn,
        string bodyAr,
        string bodyEn,
        string? type = null,
        string? data = null,
        CancellationToken cancellationToken = default);
}
