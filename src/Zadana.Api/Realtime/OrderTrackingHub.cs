using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Delivery.Enums;

namespace Zadana.Api.Realtime;

/// <summary>
/// Real-time hub dedicated to order tracking. Customers, the assigned driver,
/// the order's vendor, and admins can join the order's group to receive live
/// location updates and status changes for the same order.
/// </summary>
[Authorize]
public sealed class OrderTrackingHub : Hub
{
    public const string HubRoute = "/hubs/order-tracking";
    public const string AdminsGroup = "order-tracking-admins";

    public const string ReceiveDriverLocationMethod = "ReceiveDriverLocation";
    public const string ReceiveOrderStatusChangedMethod = "ReceiveOrderTrackingStatusChanged";
    public const string ReceiveDriverArrivalStateChangedMethod = "ReceiveOrderTrackingArrivalState";

    private readonly ICurrentUserService _currentUserService;
    private readonly IApplicationDbContext _dbContext;
    private readonly ILogger<OrderTrackingHub> _logger;

    public OrderTrackingHub(
        ICurrentUserService currentUserService,
        IApplicationDbContext dbContext,
        ILogger<OrderTrackingHub> logger)
    {
        _currentUserService = currentUserService;
        _dbContext = dbContext;
        _logger = logger;
    }

    public static string GetOrderGroup(Guid orderId) => $"order-{orderId:N}";

    public override async Task OnConnectedAsync()
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            _logger.LogWarning("OrderTrackingHub aborted connection {ConnectionId}: missing user id claim.", Context.ConnectionId);
            Context.Abort();
            return;
        }

        if (IsAdmin())
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, AdminsGroup);
            _logger.LogInformation(
                "OrderTrackingHub: admin {UserId} joined the global admin group ({ConnectionId}).",
                userId.Value, Context.ConnectionId);
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Subscribe the current connection to the live updates of a specific order.
    /// The server validates that the caller is allowed to view the order before
    /// adding the connection to the order's group.
    /// </summary>
    public async Task SubscribeToOrder(Guid orderId)
    {
        var userId = _currentUserService.UserId
            ?? throw new HubException("UNAUTHENTICATED");

        if (orderId == Guid.Empty)
        {
            throw new HubException("INVALID_ORDER_ID");
        }

        var allowed = await IsAuthorizedForOrderAsync(orderId, userId, Context.ConnectionAborted);
        if (!allowed)
        {
            _logger.LogWarning(
                "OrderTrackingHub: user {UserId} ({Role}) was denied subscription to order {OrderId}.",
                userId, _currentUserService.Role, orderId);
            throw new HubException("FORBIDDEN_ORDER_TRACKING");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GetOrderGroup(orderId));
        _logger.LogInformation(
            "OrderTrackingHub: user {UserId} ({Role}) subscribed to order {OrderId}.",
            userId, _currentUserService.Role, orderId);
    }

    /// <summary>
    /// Remove the current connection from the order group.
    /// </summary>
    public async Task UnsubscribeFromOrder(Guid orderId)
    {
        if (orderId == Guid.Empty)
        {
            return;
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetOrderGroup(orderId));
    }

    private async Task<bool> IsAuthorizedForOrderAsync(Guid orderId, Guid userId, CancellationToken cancellationToken)
    {
        if (IsAdmin())
        {
            return true;
        }

        var role = _currentUserService.Role;

        if (string.Equals(role, "Customer", StringComparison.OrdinalIgnoreCase))
        {
            return await _dbContext.Orders
                .AsNoTracking()
                .AnyAsync(o => o.Id == orderId && o.UserId == userId, cancellationToken);
        }

        if (string.Equals(role, "Vendor", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, "VendorStaff", StringComparison.OrdinalIgnoreCase))
        {
            return await _dbContext.Orders
                .AsNoTracking()
                .AnyAsync(o => o.Id == orderId && o.Vendor.UserId == userId, cancellationToken);
        }

        if (string.Equals(role, "Driver", StringComparison.OrdinalIgnoreCase))
        {
            return await _dbContext.DeliveryAssignments
                .AsNoTracking()
                .AnyAsync(a =>
                    a.OrderId == orderId &&
                    a.DriverId != null &&
                    a.Driver!.UserId == userId &&
                    a.Status != AssignmentStatus.Rejected &&
                    a.Status != AssignmentStatus.Cancelled,
                    cancellationToken);
        }

        return false;
    }

    private bool IsAdmin() =>
        string.Equals(_currentUserService.Role, "Admin", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(_currentUserService.Role, "SuperAdmin", StringComparison.OrdinalIgnoreCase);
}
