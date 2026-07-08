using Zadana.Domain.Modules.Orders.Enums;

namespace Zadana.Application.Modules.Orders.Support;

public static class OrderStatusLocalization
{
    public static LocalizedText Localize(OrderStatus status) => status switch
    {
        OrderStatus.PendingPayment => new("بانتظار الدفع", "Pending Payment"),
        OrderStatus.Placed => new("مُرسل", "Placed"),
        OrderStatus.PendingVendorAcceptance => new("بانتظار قبول المتجر", "Pending Vendor Acceptance"),
        OrderStatus.Accepted => new("مقبول", "Accepted"),
        OrderStatus.Preparing => new("قيد التجهيز", "Preparing"),
        OrderStatus.ReadyForPickup => new("جاهز للاستلام", "Ready for Pickup"),
        OrderStatus.DriverAssignmentInProgress => new("جاري البحث عن مندوب", "Driver Assignment in Progress"),
        OrderStatus.DriverAssigned => new("عيّنا المندوب", "Driver Assigned"),
        OrderStatus.PickedUp => new("استلمنا", "Picked Up"),
        OrderStatus.OnTheWay => new("في الطريق", "On The Way"),
        OrderStatus.Delivered => new("وصلنا", "Delivered"),
        OrderStatus.Cancelled => new("ملغى", "Cancelled"),
        OrderStatus.VendorRejected => new("مرفوض من المتجر", "Vendor Rejected"),
        OrderStatus.DeliveryFailed => new("فشل التوصيل", "Delivery Failed"),
        OrderStatus.Refunded => new("مسترجع", "Refunded"),
        _ => new(status.ToString(), status.ToString())
    };
}
