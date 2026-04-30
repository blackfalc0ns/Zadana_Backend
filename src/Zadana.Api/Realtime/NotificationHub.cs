using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace Zadana.Api.Realtime;

[Authorize]
public class NotificationHub : Hub
{
    public const string HubRoute = "/hubs/notifications";
    public const string CustomersGroupPrefix = "customer-";
    public const string ReceiveNotificationMethod = "ReceiveNotification";
    public const string ReceiveBroadcastMethod = "ReceiveBroadcast";
    public const string ReceiveOrderStatusChangedMethod = "ReceiveOrderStatusChanged";
    public const string ReceiveOrderSupportCaseChangedMethod = "ReceiveOrderSupportCaseChanged";
    public const string ReceiveDriverArrivalStateChangedMethod = "ReceiveDriverArrivalStateChanged";
    public const string ReceiveDeliveryOfferMethod = "ReceiveDeliveryOffer";
    public const string ReceiveAssignmentUpdatedMethod = "ReceiveAssignmentUpdated";

    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = ResolveUserId();
        if (!userId.HasValue)
        {
            _logger.LogWarning(
                "NotificationHub connection {ConnectionId} aborted because user id claim is missing.",
                Context.ConnectionId);

            Context.Abort();
            return;
        }

        // Add user to their personal group for targeted notifications
        await Groups.AddToGroupAsync(Context.ConnectionId, GetUserGroup(userId.Value));
        _logger.LogInformation(
            "NotificationHub connected. ConnectionId: {ConnectionId}. UserId: {UserId}. Role: {Role}.",
            Context.ConnectionId,
            userId.Value,
            ResolveRole());

        // Add customers to the broadcast group
        if (IsCustomer())
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "all-customers");
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = ResolveUserId();
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

    private Guid? ResolveUserId()
    {
        var idClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                   ?? Context.User?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

        return Guid.TryParse(idClaim, out var userId) ? userId : null;
    }

    private string? ResolveRole() =>
        Context.User?.FindFirst(ClaimTypes.Role)?.Value;

    private bool IsCustomer() =>
        string.Equals(ResolveRole(), "Customer", StringComparison.OrdinalIgnoreCase);
}
