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
        OrderStatus.DriverAssignmentInProgress => new("جاري البحث عن سائق", "Driver Assignment in Progress"),
        OrderStatus.DriverAssigned => new("تم تعيين السائق", "Driver Assigned"),
        OrderStatus.PickedUp => new("تم الاستلام", "Picked Up"),
        OrderStatus.OnTheWay => new("في الطريق", "On The Way"),
        OrderStatus.Delivered => new("تم التوصيل", "Delivered"),
        OrderStatus.Cancelled => new("ملغى", "Cancelled"),
        OrderStatus.VendorRejected => new("مرفوض من المتجر", "Vendor Rejected"),
        OrderStatus.DeliveryFailed => new("فشل التوصيل", "Delivery Failed"),
        OrderStatus.Refunded => new("مسترد", "Refunded"),
        _ => new(status.ToString(), status.ToString())
    };
}
