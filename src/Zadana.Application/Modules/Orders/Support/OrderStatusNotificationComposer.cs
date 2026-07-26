using System.Text.Json;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Social.Enums;

namespace Zadana.Application.Modules.Orders.Support;

internal static class OrderStatusNotificationComposer
{
    public static CustomerOrderStatusNotification ComposeCustomer(
        Guid orderId,
        Guid vendorId,
        string orderNumber,
        OrderStatus oldStatus,
        OrderStatus newStatus,
        string? actorRole,
        FulfillmentType fulfillment = FulfillmentType.Delivery)
    {
        var action = ResolveAction(newStatus);
        var targetUrl = ResolveTargetUrl(orderId);
        var type = ResolveNotificationType(newStatus, fulfillment);
        var (titleAr, titleEn, bodyAr, bodyEn) = GetCustomerNotificationContent(newStatus, orderNumber, fulfillment);

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
        OrderStatus status,
        string orderNumber,
        FulfillmentType fulfillment)
    {
        return status switch
        {
            OrderStatus.Placed => (
                "أكدنا الطلب",
                "Order Confirmed",
                $"أكدنا طلبك رقم {orderNumber} بنجاح",
                $"Your order #{orderNumber} has been confirmed successfully"),

            OrderStatus.PendingVendorAcceptance => (
                "بانتظار قبول التاجر",
                "Awaiting Vendor Approval",
                $"طلبك رقم {orderNumber} بانتظار قبول التاجر",
                $"Your order #{orderNumber} is awaiting vendor approval"),

            OrderStatus.Accepted => (
                "قبل التاجر الطلب",
                "Order Accepted",
                $"قبل التاجر طلبك رقم {orderNumber}",
                $"Your order #{orderNumber} has been accepted by the vendor"),

            OrderStatus.VendorRejected => (
                "رفض التاجر الطلب",
                "Order Rejected",
                $"للأسف، رفض التاجر طلبك رقم {orderNumber}",
                $"Sorry, your order #{orderNumber} has been rejected by the vendor"),

            OrderStatus.Preparing => (
                "المتجر يجهّز الطلب",
                "Order Being Prepared",
                $"طلبك رقم {orderNumber} ينجهز الآن",
                $"Your order #{orderNumber} is now being prepared"),

            OrderStatus.ReadyForPickup when fulfillment == FulfillmentType.Pickup => (
                "الطلب جاهز للاستلام",
                "Order Ready for Pickup",
                $"طلبك رقم {orderNumber} جاهز للاستلام من الفرع. افتح تفاصيل الطلب لعرض رمز الاستلام.",
                $"Your order #{orderNumber} is ready for pickup at the branch. Open order details to view your pickup code."),

            OrderStatus.ReadyForPickup => (
                "الطلب جاهز للاستلام",
                "Order Ready for Pickup",
                $"طلبك رقم {orderNumber} جاهز وبانتظار المندوب",
                $"Your order #{orderNumber} is ready and waiting for the driver"),

            OrderStatus.DriverAssigned => (
                "عيّنا مندوب التوصيل",
                "Driver Assigned",
                $"عيّنا مندوب يوصل طلبك رقم {orderNumber}",
                $"A driver has been assigned to deliver your order #{orderNumber}"),

            OrderStatus.PickedUp => (
                "استلم المندوب الطلب",
                "Order Picked Up",
                $"المندوب استلم طلبك رقم {orderNumber} من التاجر",
                $"The driver has picked up your order #{orderNumber} from the vendor"),

            OrderStatus.OnTheWay => (
                "الطلب في الطريق إليك",
                "Order On The Way",
                $"طلبك رقم {orderNumber} في الطريق إليك الآن!",
                $"Your order #{orderNumber} is on its way to you!"),

            OrderStatus.Delivered => (
                "وصل طلبك بنجاح",
                "Order Delivered",
                $"وصل طلبك رقم {orderNumber} بنجاح. شكرًا لك!",
                $"Your order #{orderNumber} has been delivered successfully. Thank you!"),

            OrderStatus.DeliveryFailed => (
                "فشل التوصيل",
                "Delivery Failed",
                $"للأسف، ما قدرنا نوصل طلبك رقم {orderNumber}. راح نتواصل معك",
                $"Sorry, delivery of your order #{orderNumber} failed. We will contact you"),

            OrderStatus.Cancelled => (
                "ألغينا الطلب",
                "Order Cancelled",
                fulfillment == FulfillmentType.Pickup
                    ? $"ألغينا طلب الاستلام رقم {orderNumber}"
                    : $"ألغينا طلبك رقم {orderNumber}",
                fulfillment == FulfillmentType.Pickup
                    ? $"Your pickup order #{orderNumber} has been cancelled"
                    : $"Your order #{orderNumber} has been cancelled"),

            OrderStatus.Refunded => (
                "استرجعنا المبلغ",
                "Order Refunded",
                $"استرجعنا مبلغ طلبك رقم {orderNumber}",
                $"Your order #{orderNumber} has been refunded"),

            _ => (
                "تحديث على الطلب",
                "Order Update",
                $"حدّثنا حالة طلبك رقم {orderNumber}",
                $"Your order #{orderNumber} status has been updated")
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
