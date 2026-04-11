using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Zadana.Api.Realtime.Contracts;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Infrastructure.Persistence;

namespace Zadana.Api.Realtime;

public sealed class CustomerPresenceService : ICustomerPresenceService
{
    private static readonly TimeSpan GracePeriod = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan HeartbeatTimeout = TimeSpan.FromSeconds(75);

    private readonly ConcurrentDictionary<string, PresenceConnectionState> _connections = new();
    private readonly ConcurrentDictionary<Guid, DateTime> _lastActivityByUser = new();
    private readonly ConcurrentDictionary<Guid, DateTime> _pendingOfflineDeadlines = new();
    private readonly ConcurrentDictionary<Guid, byte> _onlineUsers = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<CustomerPresenceHub> _hubContext;
    private readonly ILogger<CustomerPresenceService> _logger;

    public CustomerPresenceService(
        IServiceScopeFactory scopeFactory,
        IHubContext<CustomerPresenceHub> hubContext,
        ILogger<CustomerPresenceService> logger)
    {
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _logger = logger;
    }

    public bool IsOnline(Guid userId) => _onlineUsers.ContainsKey(userId);

    public DateTime? GetLastActivityAtUtc(Guid userId)
        => _lastActivityByUser.TryGetValue(userId, out var lastActivityAtUtc) ? lastActivityAtUtc : null;

    public Task RegisterCustomerConnectionAsync(Guid userId, string connectionId, CancellationToken cancellationToken = default)
    {
        _connections[connectionId] = new PresenceConnectionState(userId, false, DateTime.UtcNow);
        _lastActivityByUser[userId] = DateTime.UtcNow;
        _pendingOfflineDeadlines.TryRemove(userId, out _);
        return Task.CompletedTask;
    }

    public async Task MarkCustomerForegroundAsync(Guid userId, string connectionId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var wasOnline = IsOnline(userId);

        _connections.AddOrUpdate(
            connectionId,
            _ => new PresenceConnectionState(userId, true, now),
            (_, current) => current with { UserId = userId, IsForeground = true, LastHeartbeatAtUtc = now });

        _lastActivityByUser[userId] = now;
        _pendingOfflineDeadlines.TryRemove(userId, out _);
        _onlineUsers[userId] = 0;

        if (!wasOnline)
        {
            await PersistPresenceAsync(userId, PresenceState.Online, now, cancellationToken);
            await BroadcastAsync(new CustomerPresenceUpdatedDto(userId, true, now), cancellationToken);
        }
    }

    public Task MarkCustomerBackgroundAsync(Guid userId, string connectionId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        _connections.AddOrUpdate(
            connectionId,
            _ => new PresenceConnectionState(userId, false, now),
            (_, current) => current with { IsForeground = false, LastHeartbeatAtUtc = now });

        _lastActivityByUser[userId] = now;
        ScheduleOfflineIfNeeded(userId, now);
        return Task.CompletedTask;
    }

    public Task RefreshCustomerHeartbeatAsync(Guid userId, string connectionId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        _connections.AddOrUpdate(
            connectionId,
            _ => new PresenceConnectionState(userId, true, now),
            (_, current) => current with { IsForeground = true, LastHeartbeatAtUtc = now });

        _lastActivityByUser[userId] = now;
        _pendingOfflineDeadlines.TryRemove(userId, out _);
        _onlineUsers[userId] = 0;

        return Task.CompletedTask;
    }

    public Task HandleCustomerDisconnectAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        if (!_connections.TryRemove(connectionId, out var connection))
        {
            return Task.CompletedTask;
        }

        _lastActivityByUser[connection.UserId] = connection.LastHeartbeatAtUtc;
        ScheduleOfflineIfNeeded(connection.UserId, connection.LastHeartbeatAtUtc);
        return Task.CompletedTask;
    }

    public async Task SweepAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        foreach (var entry in _connections.ToArray())
        {
            if (!entry.Value.IsForeground || now - entry.Value.LastHeartbeatAtUtc <= HeartbeatTimeout)
            {
                continue;
            }

            _connections[entry.Key] = entry.Value with { IsForeground = false };
            _lastActivityByUser[entry.Value.UserId] = entry.Value.LastHeartbeatAtUtc;
            ScheduleOfflineIfNeeded(entry.Value.UserId, entry.Value.LastHeartbeatAtUtc);
        }

        foreach (var pending in _pendingOfflineDeadlines.ToArray())
        {
            if (pending.Value > now || HasForegroundConnection(pending.Key))
            {
                if (HasForegroundConnection(pending.Key))
                {
                    _pendingOfflineDeadlines.TryRemove(pending.Key, out _);
                }

                continue;
            }

            _pendingOfflineDeadlines.TryRemove(pending.Key, out _);

            if (!_onlineUsers.TryRemove(pending.Key, out _))
            {
                continue;
            }

            var lastSeenAtUtc = GetLastActivityAtUtc(pending.Key) ?? now;
            await PersistPresenceAsync(pending.Key, PresenceState.Offline, lastSeenAtUtc, cancellationToken);
            await BroadcastAsync(new CustomerPresenceUpdatedDto(pending.Key, false, lastSeenAtUtc), cancellationToken);
        }
    }

    private bool HasForegroundConnection(Guid userId)
        => _connections.Values.Any(connection => connection.UserId == userId && connection.IsForeground);

    private void ScheduleOfflineIfNeeded(Guid userId, DateTime referenceTimeUtc)
    {
        if (HasForegroundConnection(userId))
        {
            _pendingOfflineDeadlines.TryRemove(userId, out _);
            return;
        }

        _pendingOfflineDeadlines[userId] = referenceTimeUtc.Add(GracePeriod);
    }

    private async Task PersistPresenceAsync(Guid userId, PresenceState state, DateTime lastSeenAtUtc, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await dbContext.Users.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
            if (user is null)
            {
                return;
            }

            if (state == PresenceState.Online)
            {
                user.MarkPresenceOnline(lastSeenAtUtc);
            }
            else
            {
                user.MarkPresenceOffline(lastSeenAtUtc);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist customer presence for user {UserId}.", userId);
        }
    }

    private Task BroadcastAsync(CustomerPresenceUpdatedDto payload, CancellationToken cancellationToken)
        => _hubContext.Clients.Group(CustomerPresenceHub.AdminsGroup)
            .SendAsync("customerPresenceUpdated", payload, cancellationToken);

    private sealed record PresenceConnectionState(Guid UserId, bool IsForeground, DateTime LastHeartbeatAtUtc);
}
