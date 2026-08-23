using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Zadana.Api.Realtime.Contracts;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Geography;
using Zadana.Application.Modules.Orders.Support;
using Zadana.Application.Modules.Vendors.Support;
using Zadana.Domain.Modules.Orders.Enums;

namespace Zadana.Api.Realtime;

public sealed class OrderTrackingRealtimeNotifier : IOrderTrackingRealtimeNotifier
{
    private readonly IHubContext<OrderTrackingHub> _hubContext;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrderTrackingRealtimeNotifier> _logger;

    public OrderTrackingRealtimeNotifier(
        IHubContext<OrderTrackingHub> hubContext,
        IServiceScopeFactory scopeFactory,
        ILogger<OrderTrackingRealtimeNotifier> logger)
    {
        _hubContext = hubContext;
        _scopeFactory = scopeFactory;
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
            await SignalRDispatch.SendToGroupAsync(
                _hubContext,
                groupName,
                OrderTrackingHub.ReceiveDriverLocationMethod,
                payload,
                _logger,
                "driver-location",
                cancellationToken);

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
        Guid customerUserId,
        OrderStatus oldStatus,
        OrderStatus newStatus,
        string? actorRole,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var action = ResolveAction(newStatus);
            var targetUrl = $"/orders/{orderId}";
            var changedAtUtc = DateTime.UtcNow;
            var pickupContext = await LoadPickupContextAsync(orderId, cancellationToken);

            var sharedPayload = BuildPayload(
                orderId,
                orderNumber,
                vendorId,
                oldStatus,
                newStatus,
                actorRole,
                action,
                targetUrl,
                changedAtUtc,
                pickupContext,
                includeCustomerPickupSecrets: false,
                showPopup: false);

            await SignalRDispatch.SendToGroupAsync(
                _hubContext,
                OrderTrackingHub.GetOrderGroup(orderId),
                OrderTrackingHub.ReceiveOrderStatusChangedMethod,
                sharedPayload,
                _logger,
                "tracking-status",
                cancellationToken);

            // Never send ReceiveOrderStatusChanged on NotificationHub for customers.
            // The customer app turns that event into a second OS banner ("تحديث على الطلب" + GUID)
            // for Preparing / ReadyForPickup / DriverAssigned / Delivered — even with showPopup=false.
            // Visible customer copy is OneSignal-only (OrderStatusNotificationDispatcher).
            // Pickup OTP stays on the order details API / tracking refresh, not a parallel banner event.
            _ = customerUserId;
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

            await SignalRDispatch.SendToGroupAsync(
                _hubContext,
                OrderTrackingHub.GetOrderGroup(orderId),
                OrderTrackingHub.ReceiveDriverArrivalStateChangedMethod,
                payload,
                _logger,
                "tracking-arrival",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to broadcast driver arrival state for order {OrderId}",
                orderId);
        }
    }

    private async Task<PickupBroadcastContext> LoadPickupContextAsync(Guid orderId, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var order = await dbContext.Orders
            .AsNoTracking()
            .Where(item => item.Id == orderId)
            .Select(item => new
            {
                item.Fulfillment,
                item.Status,
                item.PickupOtpCode,
                item.PickupOtpExpiresAtUtc,
                item.PickupOtpVerifiedAtUtc,
                item.PickupNoShowDeadlineUtc,
                item.VendorBranchId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (order is null || order.Fulfillment != FulfillmentType.Pickup)
        {
            return PickupBroadcastContext.Empty;
        }

        OrderPickupBranchRealtimePayload? branchPayload = null;
        if (order.VendorBranchId.HasValue)
        {
            var branch = await dbContext.VendorBranches
                .AsNoTracking()
                .Include(item => item.Vendor)
                .Include(item => item.OperatingHours)
                .FirstOrDefaultAsync(item => item.Id == order.VendorBranchId.Value, cancellationToken);

            if (branch is not null)
            {
                var address = SaudiGeographyDisplay.FormatBranchAddress(
                    branch.AddressLine,
                    branch.City,
                    branch.Region);

                branchPayload = new OrderPickupBranchRealtimePayload(
                    VendorDisplayNames.ResolvePickupBranchName(branch),
                    address,
                    BranchOperatingHoursSupport.BuildHoursTodayLabel(branch.OperatingHours.ToList(), DateTime.UtcNow));
            }
        }

        var includeCustomerPickupSecrets = order.Status == OrderStatus.ReadyForPickup &&
            !order.PickupOtpVerifiedAtUtc.HasValue;

        return new PickupBroadcastContext(
            order.Fulfillment.ToString(),
            includeCustomerPickupSecrets ? order.PickupOtpCode : null,
            includeCustomerPickupSecrets ? order.PickupOtpExpiresAtUtc : null,
            order.PickupNoShowDeadlineUtc,
            branchPayload,
            includeCustomerPickupSecrets);
    }

    private static OrderStatusChangedRealtimePayload BuildPayload(
        Guid orderId,
        string orderNumber,
        Guid vendorId,
        OrderStatus oldStatus,
        OrderStatus newStatus,
        string? actorRole,
        string action,
        string targetUrl,
        DateTime changedAtUtc,
        PickupBroadcastContext pickupContext,
        bool includeCustomerPickupSecrets,
        bool showPopup = true)
    {
        var fulfillment = string.Equals(pickupContext.FulfillmentType, nameof(FulfillmentType.Pickup), StringComparison.OrdinalIgnoreCase)
            ? FulfillmentType.Pickup
            : FulfillmentType.Delivery;
        var normalizedFulfillment = fulfillment == FulfillmentType.Pickup ? "pickup" : "delivery";
        var normalizedOld = OrderTrackingStatusMapper.NormalizeCustomerTrackingStatus(oldStatus.ToString(), fulfillment);
        var normalizedNew = OrderTrackingStatusMapper.NormalizeCustomerTrackingStatus(newStatus.ToString(), fulfillment);
        var popupType = fulfillment == FulfillmentType.Pickup && newStatus == OrderStatus.ReadyForPickup
            ? "order_pickup_ready"
            : newStatus == OrderStatus.Refunded
                ? "order_refund_status_changed"
                : "order_status_changed";

        return new OrderStatusChangedRealtimePayload(
            orderId,
            orderNumber,
            vendorId,
            normalizedOld,
            normalizedNew,
            actorRole,
            action,
            targetUrl,
            changedAtUtc,
            Presentation: showPopup ? "popup" : "silent",
            PopupType: popupType,
            ShowPopup: showPopup,
            OldStatusRaw: oldStatus.ToString(),
            NewStatusRaw: newStatus.ToString(),
            FulfillmentType: normalizedFulfillment,
            PickupOtpCode: includeCustomerPickupSecrets ? pickupContext.PickupOtpCode : null,
            PickupOtpExpiresAtUtc: includeCustomerPickupSecrets ? pickupContext.PickupOtpExpiresAtUtc : null,
            PickupNoShowDeadlineUtc: pickupContext.PickupNoShowDeadlineUtc,
            PickupBranch: pickupContext.PickupBranch);
    }

    private static string ResolveAction(OrderStatus status) =>
        status switch
        {
            OrderStatus.PendingVendorAcceptance => "placed",
            OrderStatus.OnTheWay => "on_the_way",
            OrderStatus.Cancelled => "cancelled",
            OrderStatus.Refunded => "refunded",
            _ => "status_changed"
        };

    private sealed record PickupBroadcastContext(
        string? FulfillmentType,
        string? PickupOtpCode,
        DateTime? PickupOtpExpiresAtUtc,
        DateTime? PickupNoShowDeadlineUtc,
        OrderPickupBranchRealtimePayload? PickupBranch,
        bool IncludeCustomerPickupSecrets)
    {
        public static PickupBroadcastContext Empty { get; } = new(null, null, null, null, null, false);
    }
}
