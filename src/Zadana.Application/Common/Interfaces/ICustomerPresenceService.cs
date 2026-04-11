namespace Zadana.Application.Common.Interfaces;

public interface ICustomerPresenceService
{
    bool IsOnline(Guid userId);
    DateTime? GetLastActivityAtUtc(Guid userId);
    Task RegisterCustomerConnectionAsync(Guid userId, string connectionId, CancellationToken cancellationToken = default);
    Task MarkCustomerForegroundAsync(Guid userId, string connectionId, CancellationToken cancellationToken = default);
    Task MarkCustomerBackgroundAsync(Guid userId, string connectionId, CancellationToken cancellationToken = default);
    Task RefreshCustomerHeartbeatAsync(Guid userId, string connectionId, CancellationToken cancellationToken = default);
    Task HandleCustomerDisconnectAsync(string connectionId, CancellationToken cancellationToken = default);
}
