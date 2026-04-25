using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Zadana.Application.Common.Interfaces;

namespace Zadana.Api.Realtime;

[Authorize]
public class NotificationHub : Hub
{
    public const string HubRoute = "/hubs/notifications";
    public const string CustomersGroupPrefix = "customer-";
    public const string ReceiveNotificationMethod = "ReceiveNotification";
    public const string ReceiveBroadcastMethod = "ReceiveBroadcast";
    public const string ReceiveOrderStatusChangedMethod = "ReceiveOrderStatusChanged";
    public const string ReceiveDriverArrivalStateChangedMethod = "ReceiveDriverArrivalStateChanged";

    private readonly ICurrentUserService _currentUserService;

    public NotificationHub(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            Context.Abort();
            return;
        }

        // Add user to their personal group for targeted notifications
        await Groups.AddToGroupAsync(Context.ConnectionId, GetUserGroup(userId.Value));

        // Add customers to the broadcast group
        if (IsCustomer())
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "all-customers");
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = _currentUserService.UserId;
        if (userId.HasValue)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetUserGroup(userId.Value));

            if (IsCustomer())
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, "all-customers");
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    public static string GetUserGroup(Guid userId) => $"{CustomersGroupPrefix}{userId}";

    private bool IsCustomer() =>
        string.Equals(_currentUserService.Role, "Customer", StringComparison.OrdinalIgnoreCase);
}
