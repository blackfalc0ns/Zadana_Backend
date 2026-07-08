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
        string? actorRole)
    {
        var action = ResolveAction(newStatus);
        var targetUrl = ResolveTargetUrl(orderId);
        var type = newStatus == OrderStatus.Cancelled
            ? NotificationTypes.OrderCancelled
            : NotificationTypes.OrderStatusChanged;
        var (titleAr, titleEn, bodyAr, bodyEn) = GetCustomerNotificationContent(newStatus, orderNumber);

        return new CustomerOrderStatusNotification(
            titleAr,
            titleEn,
            bodyAr,
            bodyEn,
            type,
            BuildData(orderId, orderNumber, vendorId, oldStatus, newStatus, actorRole, action, targetUrl),
            action,
            targetUrl);
    }

    public static string BuildData(
        Guid orderId,
        string orderNumber,
        Guid vendorId,
        OrderStatus oldStatus,
        OrderStatus newStatus,
        string? actorRole,
        string action,
        string targetUrl)
    {
        var isRefundUpdate = newStatus == OrderStatus.Refunded;
        var popupType = isRefundUpdate ? "order_refund_status_changed" : "order_status_changed";
        var eventName = isRefundUpdate
            ? "order.refund.refunded"
            : $"order.status.{newStatus.ToString().ToLowerInvariant()}";
        var dedupeKey = $"order-status:{orderId:N}:{oldStatus}:{newStatus}";

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
            ["eventName"] = eventName
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
        string orderNumber)
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
                $"ألغينا طلبك رقم {orderNumber}",
                $"Your order #{orderNumber} has been cancelled"),

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
