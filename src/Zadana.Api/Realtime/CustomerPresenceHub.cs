using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Zadana.Application.Common.Interfaces;

namespace Zadana.Api.Realtime;

[Authorize]
public class CustomerPresenceHub : Hub
{
    public const string HubRoute = "/hubs/customer-presence";
    public const string AdminsGroup = "customer-presence-admins";

    private readonly ICurrentUserService _currentUserService;
    private readonly ICustomerPresenceService _customerPresenceService;

    public CustomerPresenceHub(
        ICurrentUserService currentUserService,
        ICustomerPresenceService customerPresenceService)
    {
        _currentUserService = currentUserService;
        _customerPresenceService = customerPresenceService;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            Context.Abort();
            return;
        }

        if (IsAdmin())
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, AdminsGroup);
        }

        if (IsCustomer())
        {
            await _customerPresenceService.RegisterCustomerConnectionAsync(userId.Value, Context.ConnectionId, Context.ConnectionAborted);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (IsCustomer())
        {
            await _customerPresenceService.HandleCustomerDisconnectAsync(Context.ConnectionId, CancellationToken.None);
        }

        if (IsAdmin())
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, AdminsGroup);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public Task AppForeground()
    {
        EnsureCustomerConnection();
        return _customerPresenceService.MarkCustomerForegroundAsync(_currentUserService.UserId!.Value, Context.ConnectionId, Context.ConnectionAborted);
    }

    public Task AppBackground()
    {
        EnsureCustomerConnection();
        return _customerPresenceService.MarkCustomerBackgroundAsync(_currentUserService.UserId!.Value, Context.ConnectionId, Context.ConnectionAborted);
    }

    public Task Heartbeat()
    {
        EnsureCustomerConnection();
        return _customerPresenceService.RefreshCustomerHeartbeatAsync(_currentUserService.UserId!.Value, Context.ConnectionId, Context.ConnectionAborted);
    }

    private bool IsCustomer() => string.Equals(_currentUserService.Role, "Customer", StringComparison.OrdinalIgnoreCase);

    private bool IsAdmin() =>
        string.Equals(_currentUserService.Role, "Admin", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(_currentUserService.Role, "SuperAdmin", StringComparison.OrdinalIgnoreCase);

    private void EnsureCustomerConnection()
    {
        if (!_currentUserService.UserId.HasValue || !IsCustomer())
        {
            throw new HubException("Only authenticated customers can update presence.");
        }
    }
}
