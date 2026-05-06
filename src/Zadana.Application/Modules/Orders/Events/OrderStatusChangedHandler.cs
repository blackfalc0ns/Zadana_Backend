using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.Support;
using Zadana.Application.Modules.Orders.Support;
using Zadana.Application.Modules.Wallets.Services;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Domain.Modules.Social.Enums;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;

namespace Zadana.Application.Modules.Orders.Events;

public class OrderStatusChangedHandler : INotificationHandler<OrderStatusChangedNotification>
{
    private readonly INotificationService _notificationService;
    private readonly IApplicationDbContext _context;
    private readonly IOneSignalPushService _oneSignalPushService;
    private readonly IOrderStatusNotificationDispatcher _orderStatusNotificationDispatcher;
    private readonly OrderRevenueDistributionService _revenueDistributionService;
    private readonly ILogger<OrderStatusChangedHandler> _logger;

    public OrderStatusChangedHandler(
        INotificationService notificationService,
        IApplicationDbContext context,
        IOneSignalPushService oneSignalPushService,
        IOrderStatusNotificationDispatcher orderStatusNotificationDispatcher,
        OrderRevenueDistributionService revenueDistributionService,
        ILogger<OrderStatusChangedHandler> logger)
    {
        _notificationService = notificationService;
        _context = context;
        _oneSignalPushService = oneSignalPushService;
        _orderStatusNotificationDispatcher = orderStatusNotificationDispatcher;
        _revenueDistributionService = revenueDistributionService;
        _logger = logger;
    }

    public async Task Handle(OrderStatusChangedNotification notification, CancellationToken cancellationToken)
    {
        await _revenueDistributionService.DistributeAsync(notification.OrderId, cancellationToken);

        var targetUrl = OrderStatusNotificationComposer.ResolveTargetUrl(notification.OrderId);
        var action = OrderStatusNotificationComposer.ResolveAction(notification.NewStatus);
        var data = OrderStatusNotificationComposer.BuildData(
            notification.OrderId,
            notification.OrderNumber,
            notification.VendorId,
            notification.OldStatus,
            notification.NewStatus,
            notification.ActorRole,
            action,
            targetUrl);

        if (notification.NotifyCustomer && !notification.CustomerNotificationAlreadySent)
        {
            await _orderStatusNotificationDispatcher.DispatchCustomerAsync(
                new OrderStatusCustomerNotificationRequest(
                    notification.UserId,
                    notification.OrderId,
                    notification.VendorId,
                    notification.OrderNumber,
                    notification.OldStatus,
                    notification.NewStatus,
                    notification.ActorRole),
                cancellationToken);
        }

        await SendRealtimeToAssignedDriverAsync(notification, action, targetUrl, cancellationToken);

        if (!notification.NotifyVendor)
        {
            return;
        }

        var vendorRecipient = await _context.Vendors
            .AsNoTracking()
            .Where(vendor => vendor.Id == notification.VendorId)
            .Select(vendor => new
            {
                vendor.UserId,
                vendor.NewOrdersNotificationsEnabled
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (vendorRecipient is null)
        {
            return;
        }

        var (vendorTitleAr, vendorTitleEn, vendorBodyAr, vendorBodyEn, vendorType) =
            GetVendorNotificationContent(notification.NewStatus, notification.OrderNumber);

        await _notificationService.SendToUserAsync(
            vendorRecipient.UserId,
            vendorTitleAr,
            vendorTitleEn,
            vendorBodyAr,
            vendorBodyEn,
            vendorType,
            notification.OrderId,
            data,
            cancellationToken);

        await _notificationService.SendOrderStatusChangedToUserAsync(
            vendorRecipient.UserId,
            notification.OrderId,
            notification.OrderNumber,
            notification.VendorId,
            notification.OldStatus.ToString(),
            notification.NewStatus.ToString(),
            notification.ActorRole,
            action,
            targetUrl,
            cancellationToken);

        if (notification.NewStatus == OrderStatus.PendingVendorAcceptance && !vendorRecipient.NewOrdersNotificationsEnabled)
        {
            return;
        }

        await _oneSignalPushService.SendToExternalUserAsync(
            vendorRecipient.UserId.ToString(),
            vendorTitleAr,
            vendorTitleEn,
            vendorBodyAr,
            vendorBodyEn,
            vendorType,
            notification.OrderId,
            data,
            targetUrl,
            cancellationToken);
    }

    private async Task SendRealtimeToAssignedDriverAsync(
        OrderStatusChangedNotification notification,
        string action,
        string targetUrl,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[DriverRealtime] Looking for active assignment for order {OrderId} (status change: {OldStatus} -> {NewStatus}, actor: {Actor})",
            notification.OrderId, notification.OldStatus, notification.NewStatus, notification.ActorRole);

        var driverAssignment = await _context.DeliveryAssignments
            .AsNoTracking()
            .Where(assignment =>
                assignment.OrderId == notification.OrderId &&
                assignment.DriverId != null &&
                assignment.Status != AssignmentStatus.SearchingDriver &&
                assignment.Status != AssignmentStatus.OfferSent &&
                assignment.Status != AssignmentStatus.Rejected &&
                assignment.Status != AssignmentStatus.Cancelled)
            .OrderByDescending(assignment => assignment.CreatedAtUtc)
            .Select(assignment => new { assignment.Id, assignment.Status, DriverUserId = assignment.Driver!.UserId })
            .FirstOrDefaultAsync(cancellationToken);

        if (driverAssignment is null)
        {
            _logger.LogWarning(
                "[DriverRealtime] NO active assignment found for order {OrderId}. ReceiveAssignmentUpdated will NOT be sent.",
                notification.OrderId);
            return;
        }

        _logger.LogInformation(
            "[DriverRealtime] Found assignment {AssignmentId} (status={AssignmentStatus}) for driver user {DriverUserId}. Sending ReceiveOrderStatusChanged + ReceiveAssignmentUpdated.",
            driverAssignment.Id, driverAssignment.Status, driverAssignment.DriverUserId);

        await SendDriverAssignmentInboxAndPushAsync(
            notification,
            driverAssignment.Id,
            driverAssignment.DriverUserId,
            cancellationToken);

        await _notificationService.SendOrderStatusChangedToUserAsync(
            driverAssignment.DriverUserId,
            notification.OrderId,
            notification.OrderNumber,
            notification.VendorId,
            notification.OldStatus.ToString(),
            notification.NewStatus.ToString(),
            notification.ActorRole,
            action,
            targetUrl,
            cancellationToken);

        // Push full assignment detail so the driver's order detail page refreshes in real-time
        await _notificationService.SendAssignmentUpdatedToDriverAsync(
            driverAssignment.DriverUserId,
            driverAssignment.Id,
            notification.OrderId,
            cancellationToken);

        await _notificationService.SendDriverHomeUpdatedAsync(
            driverAssignment.DriverUserId,
            cancellationToken);

        _logger.LogInformation(
            "[DriverRealtime] Successfully dispatched ReceiveAssignmentUpdated to driver user {DriverUserId} for assignment {AssignmentId}.",
            driverAssignment.DriverUserId, driverAssignment.Id);
    }

    private async Task SendDriverAssignmentInboxAndPushAsync(
        OrderStatusChangedNotification notification,
        Guid assignmentId,
        Guid driverUserId,
        CancellationToken cancellationToken)
    {
        if (string.Equals(notification.ActorRole, "driver", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var envelope = TryComposeDriverAssignmentNotification(notification, assignmentId, driverUserId);
        if (envelope is null)
        {
            return;
        }

        await _notificationService.SendToUserAsync(
            driverUserId,
            envelope.Request,
            cancellationToken);

        await envelope.PushRequest.DispatchAsync(_oneSignalPushService, cancellationToken);
    }

    private static DriverAssignmentNotificationEnvelope? TryComposeDriverAssignmentNotification(
        OrderStatusChangedNotification notification,
        Guid assignmentId,
        Guid driverUserId)
    {
        var screen = "assignment_detail";

        return notification.NewStatus switch
            {
                OrderStatus.DriverAssigned => CreateDriverAssignmentNotification(
                    notification,
                    assignmentId,
                    driverUserId,
                    screen,
                    "assignment.driver_assigned",
                "تم تعيين طلب جديد لك",
                "A delivery was assigned to you",
                $"تم تعيين الطلب رقم #{notification.OrderNumber} لك. افتح تفاصيل المهمة لبدء التنفيذ.",
                $"Order #{notification.OrderNumber} was assigned to you. Open the assignment to get started.",
                NotificationPriorities.High,
                OneSignalPushRequestKind.Standard),

                OrderStatus.ReadyForPickup => CreateDriverAssignmentNotification(
                    notification,
                    assignmentId,
                    driverUserId,
                    screen,
                    "assignment.pickup_ready",
                "الطلب جاهز للاستلام",
                "Order ready for pickup",
                $"الطلب رقم #{notification.OrderNumber} أصبح جاهزًا للاستلام من التاجر.",
                $"Order #{notification.OrderNumber} is now ready for pickup.",
                NotificationPriorities.High,
                OneSignalPushRequestKind.Standard),

                OrderStatus.Cancelled => CreateDriverAssignmentNotification(
                    notification,
                    assignmentId,
                    driverUserId,
                    screen,
                    "assignment.active_order_cancelled",
                "تم إلغاء الطلب الحالي",
                "Active order cancelled",
                $"تم إلغاء الطلب رقم #{notification.OrderNumber}.",
                $"Order #{notification.OrderNumber} was cancelled.",
                NotificationPriorities.Critical,
                OneSignalPushRequestKind.HeadsUp),

            _ => null
        };
    }

    private static DriverAssignmentNotificationEnvelope CreateDriverAssignmentNotification(
        OrderStatusChangedNotification notification,
        Guid assignmentId,
        Guid driverUserId,
        string screen,
        string eventName,
        string titleAr,
        string titleEn,
        string bodyAr,
        string bodyEn,
        string priority,
        OneSignalPushRequestKind pushKind)
    {
        var data = DriverNotificationDataBuilder.Build(
            screen,
            eventName,
            orderId: notification.OrderId,
            assignmentId: assignmentId,
            extra: new
            {
                orderNumber = notification.OrderNumber,
                oldStatus = notification.OldStatus.ToString(),
                newStatus = notification.NewStatus.ToString(),
                actorRole = notification.ActorRole
            });

        var request = new NotificationDispatchRequest(
            titleAr,
            titleEn,
            bodyAr,
            bodyEn,
            NotificationTypes.DriverAssignmentUpdated,
            NotificationCategories.Assignment,
            priority,
            notification.OrderId,
            data);

        var pushRequest = pushKind == OneSignalPushRequestKind.HeadsUp
            ? OneSignalMobilePushRequest.CreateHeadsUp(
                driverUserId.ToString(),
                titleAr,
                titleEn,
                bodyAr,
                bodyEn,
                NotificationTypes.DriverAssignmentUpdated,
                notification.OrderId,
                data,
                category: NotificationCategories.Assignment)
            : OneSignalMobilePushRequest.CreateStandard(
                driverUserId.ToString(),
                titleAr,
                titleEn,
                bodyAr,
                bodyEn,
                NotificationTypes.DriverAssignmentUpdated,
                notification.OrderId,
                data,
                category: NotificationCategories.Assignment);

        return new DriverAssignmentNotificationEnvelope(request, pushRequest);
    }


    private sealed record DriverAssignmentNotificationEnvelope(
        NotificationDispatchRequest Request,
        OneSignalMobilePushRequest PushRequest);

    private enum OneSignalPushRequestKind
    {
        Standard,
        HeadsUp
    }

    private static (string TitleAr, string TitleEn, string BodyAr, string BodyEn, string Type) GetVendorNotificationContent(
        OrderStatus status,
        string orderNumber)
    {
        return status switch
        {
            OrderStatus.PendingVendorAcceptance => (
                "طلب جديد بانتظار التأكيد",
                "New order awaiting confirmation",
                $"يوجد طلب جديد رقم {orderNumber} بانتظار موافقتك.",
                $"Order #{orderNumber} is waiting for your confirmation.",
                NotificationTypes.VendorNewOrder),
            OrderStatus.Cancelled => (
                "تم إلغاء الطلب",
                "Order cancelled",
                $"قام العميل بإلغاء الطلب رقم {orderNumber}.",
                $"The customer cancelled order #{orderNumber}.",
                NotificationTypes.OrderCancelled),
            OrderStatus.DeliveryFailed => (
                "تعذر تسليم الطلب",
                "Delivery failed",
                $"تعذر تسليم الطلب رقم {orderNumber} ويحتاج إلى متابعة.",
                $"Delivery failed for order #{orderNumber} and needs follow-up.",
                NotificationTypes.OrderStatusChanged),
            _ => (
                "تحديث على الطلب",
                "Order update",
                $"تم تحديث الطلب رقم {orderNumber}.",
                $"Order #{orderNumber} has been updated.",
                NotificationTypes.OrderStatusChanged)
        };
    }
}
