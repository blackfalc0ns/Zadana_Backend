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
        string targetUrl) =>
        JsonSerializer.Serialize(new
        {
            orderId,
            orderNumber,
            vendorId,
            oldStatus = oldStatus.ToString(),
            newStatus = newStatus.ToString(),
            actorRole,
            action,
            targetUrl
        });

    public static string ResolveAction(OrderStatus newStatus) =>
        newStatus switch
        {
            OrderStatus.PendingVendorAcceptance => "placed",
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
                "تم تأكيد الطلب",
                "Order Confirmed",
                $"تم تأكيد طلبك رقم {orderNumber} بنجاح",
                $"Your order #{orderNumber} has been confirmed successfully"),

            OrderStatus.PendingVendorAcceptance => (
                "في انتظار موافقة التاجر",
                "Awaiting Vendor Approval",
                $"طلبك رقم {orderNumber} في انتظار موافقة التاجر",
                $"Your order #{orderNumber} is awaiting vendor approval"),

            OrderStatus.Accepted => (
                "تم قبول الطلب",
                "Order Accepted",
                $"تم قبول طلبك رقم {orderNumber} من قبل التاجر",
                $"Your order #{orderNumber} has been accepted by the vendor"),

            OrderStatus.VendorRejected => (
                "تم رفض الطلب",
                "Order Rejected",
                $"للأسف، تم رفض طلبك رقم {orderNumber} من قبل التاجر",
                $"Sorry, your order #{orderNumber} has been rejected by the vendor"),

            OrderStatus.Preparing => (
                "جاري تحضير الطلب",
                "Order Being Prepared",
                $"طلبك رقم {orderNumber} قيد التحضير الآن",
                $"Your order #{orderNumber} is now being prepared"),

            OrderStatus.ReadyForPickup => (
                "الطلب جاهز للاستلام",
                "Order Ready for Pickup",
                $"طلبك رقم {orderNumber} جاهز وفي انتظار المندوب",
                $"Your order #{orderNumber} is ready and waiting for the driver"),

            OrderStatus.DriverAssigned => (
                "تم تعيين مندوب التوصيل",
                "Driver Assigned",
                $"تم تعيين مندوب لتوصيل طلبك رقم {orderNumber}",
                $"A driver has been assigned to deliver your order #{orderNumber}"),

            OrderStatus.PickedUp => (
                "تم استلام الطلب من التاجر",
                "Order Picked Up",
                $"المندوب استلم طلبك رقم {orderNumber} من التاجر",
                $"The driver has picked up your order #{orderNumber} from the vendor"),

            OrderStatus.OnTheWay => (
                "الطلب في الطريق إليك",
                "Order On The Way",
                $"طلبك رقم {orderNumber} في الطريق إليك الآن!",
                $"Your order #{orderNumber} is on its way to you!"),

            OrderStatus.Delivered => (
                "تم التوصيل بنجاح",
                "Order Delivered",
                $"تم توصيل طلبك رقم {orderNumber} بنجاح. شكراً لك!",
                $"Your order #{orderNumber} has been delivered successfully. Thank you!"),

            OrderStatus.DeliveryFailed => (
                "فشل التوصيل",
                "Delivery Failed",
                $"للأسف، فشل توصيل طلبك رقم {orderNumber}. سيتم التواصل معك",
                $"Sorry, delivery of your order #{orderNumber} failed. We will contact you"),

            OrderStatus.Cancelled => (
                "تم إلغاء الطلب",
                "Order Cancelled",
                $"تم إلغاء طلبك رقم {orderNumber}",
                $"Your order #{orderNumber} has been cancelled"),

            OrderStatus.Refunded => (
                "تم استرداد المبلغ",
                "Order Refunded",
                $"تم استرداد مبلغ طلبك رقم {orderNumber}",
                $"Your order #{orderNumber} has been refunded"),

            _ => (
                "تحديث على الطلب",
                "Order Update",
                $"تم تحديث حالة طلبك رقم {orderNumber}",
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
