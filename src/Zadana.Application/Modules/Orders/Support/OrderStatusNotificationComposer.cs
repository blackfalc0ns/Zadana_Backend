using System.Text.Json;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Social.Enums;

namespace Zadana.Application.Modules.Orders.Support;

internal static class OrderStatusNotificationComposer
{
    /// <summary>
    /// Customer push/inbox is limited to eight status-name notifications.
    /// Intermediate noise (assignment in progress, picked up, payment pending, etc.) is skipped.
    /// </summary>
    public static bool ShouldNotifyCustomer(OrderStatus newStatus) =>
        newStatus is OrderStatus.PendingVendorAcceptance
            or OrderStatus.Accepted
            or OrderStatus.Preparing
            or OrderStatus.ReadyForPickup
            or OrderStatus.DriverAssigned
            or OrderStatus.OnTheWay
            or OrderStatus.Delivered
            or OrderStatus.Cancelled
            or OrderStatus.VendorRejected
            or OrderStatus.DeliveryFailed
            or OrderStatus.Refunded;

    public static CustomerOrderStatusNotification? ComposeCustomer(
        Guid orderId,
        Guid vendorId,
        string orderNumber,
        OrderStatus oldStatus,
        OrderStatus newStatus,
        string? actorRole,
        FulfillmentType fulfillment = FulfillmentType.Delivery)
    {
        if (!ShouldNotifyCustomer(newStatus))
        {
            return null;
        }

        var action = ResolveAction(newStatus);
        var targetUrl = ResolveTargetUrl(orderId);
        var type = ResolveNotificationType(newStatus, fulfillment);
        var (titleAr, titleEn, bodyAr, bodyEn) = GetCustomerNotificationContent(newStatus);

        return new CustomerOrderStatusNotification(
            titleAr,
            titleEn,
            bodyAr,
            bodyEn,
            type,
            BuildData(orderId, orderNumber, vendorId, oldStatus, newStatus, actorRole, action, targetUrl, fulfillment),
            action,
            targetUrl);
    }

    public static string ResolveNotificationType(OrderStatus newStatus, FulfillmentType fulfillment)
    {
        if (newStatus == OrderStatus.Cancelled)
        {
            return NotificationTypes.OrderCancelled;
        }

        if (newStatus == OrderStatus.ReadyForPickup && fulfillment == FulfillmentType.Pickup)
        {
            return NotificationTypes.PickupReady;
        }

        return NotificationTypes.OrderStatusChanged;
    }

    public static string BuildData(
        Guid orderId,
        string orderNumber,
        Guid vendorId,
        OrderStatus oldStatus,
        OrderStatus newStatus,
        string? actorRole,
        string action,
        string targetUrl,
        FulfillmentType fulfillment = FulfillmentType.Delivery)
    {
        var isRefundUpdate = newStatus == OrderStatus.Refunded;
        var popupType = isRefundUpdate ? "order_refund_status_changed" : "order_status_changed";
        var eventName = isRefundUpdate
            ? "order.refund.refunded"
            : newStatus == OrderStatus.ReadyForPickup && fulfillment == FulfillmentType.Pickup
                ? "order.pickup.ready"
                : $"order.status.{newStatus.ToString().ToLowerInvariant()}";
        // Cancelled notifications must collapse across duplicate publish paths / retries.
        var dedupeKey = newStatus is OrderStatus.Cancelled or OrderStatus.VendorRejected
            ? $"order-cancelled:{orderId:N}"
            : $"order-status:{orderId:N}:{oldStatus}:{newStatus}";

        var data = new Dictionary<string, object?>
        {
            ["dedupeKey"] = dedupeKey,
            ["eventId"] = dedupeKey,
            ["orderId"] = orderId,
            ["orderNumber"] = orderNumber,
            ["vendorId"] = vendorId,
            ["oldStatus"] = oldStatus.ToString(),
            ["newStatus"] = newStatus.ToString(),
            ["actorRole"] = actorRole,
            ["action"] = action,
            ["targetUrl"] = targetUrl,
            ["category"] = "order",
            ["screen"] = "order_tracking",
            ["presentation"] = "popup",
            ["popupType"] = popupType,
            ["showPopup"] = true,
            ["eventName"] = eventName,
            ["fulfillmentType"] = fulfillment.ToString()
        };

        if (isRefundUpdate)
        {
            data["isRefund"] = true;
            data["refundStatus"] = "refunded";
            data["refundPopupType"] = popupType;
        }

        return JsonSerializer.Serialize(data);
    }

    public static string ResolveAction(OrderStatus newStatus) =>
        newStatus switch
        {
            OrderStatus.PendingVendorAcceptance => "placed",
            OrderStatus.OnTheWay => "on_the_way",
            OrderStatus.Cancelled => "cancelled",
            _ => "status_changed"
        };

    public static string ResolveTargetUrl(Guid orderId) => $"/orders/{orderId}";

    private static (string TitleAr, string TitleEn, string BodyAr, string BodyEn) GetCustomerNotificationContent(
        OrderStatus status)
    {
        // Title and body are the status name only (no order number / generic update copy).
        return status switch
        {
            OrderStatus.PendingVendorAcceptance => (
                "بانتظار قبول التاجر",
                "Awaiting vendor approval",
                "بانتظار قبول التاجر",
                "Awaiting vendor approval"),

            OrderStatus.Accepted => (
                "تم القبول",
                "Accepted",
                "تم القبول",
                "Accepted"),

            OrderStatus.Preparing => (
                "جاري التجهيز",
                "Preparing",
                "جاري التجهيز",
                "Preparing"),

            OrderStatus.ReadyForPickup => (
                "جاهز للاستلام",
                "Ready for pickup",
                "جاهز للاستلام",
                "Ready for pickup"),

            OrderStatus.DriverAssigned => (
                "تم تعيين المندوب",
                "Driver assigned",
                "تم تعيين المندوب",
                "Driver assigned"),

            OrderStatus.OnTheWay => (
                "في الطريق",
                "On the way",
                "في الطريق",
                "On the way"),

            OrderStatus.Delivered => (
                "تم التسليم",
                "Delivered",
                "تم التسليم",
                "Delivered"),

            OrderStatus.Cancelled or OrderStatus.VendorRejected
                or OrderStatus.DeliveryFailed or OrderStatus.Refunded => (
                "ملغي",
                "Cancelled",
                "ملغي",
                "Cancelled"),

            _ => throw new InvalidOperationException(
                $"Customer order-status notification is not configured for {status}.")
        };
    }
}

internal sealed record CustomerOrderStatusNotification(
    string TitleAr,
    string TitleEn,
    string BodyAr,
    string BodyEn,
    string NotificationType,
    string Data,
    string Action,
    string TargetUrl);
