namespace Zadana.Application.Common.Interfaces;

public interface INotificationService
{
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
