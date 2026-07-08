namespace Zadana.Application.Modules.Orders.DTOs;

public record AdminOrderFilterOptionDto(string Value, string LabelAr, string LabelEn);

public record AdminOrderFilterOptionsDto(
    IReadOnlyList<AdminOrderFilterOptionDto> OrderStatuses,
    IReadOnlyList<AdminOrderFilterOptionDto> PaymentStatuses,
    IReadOnlyList<AdminOrderFilterOptionDto> FulfillmentStatuses,
    IReadOnlyList<AdminOrderFilterOptionDto> QueueViews);

public static class AdminOrderFilterOptionsFactory
{
    public static AdminOrderFilterOptionsDto Build() => new(
        OrderStatuses:
        [
            new("NEW", "جديد", "New"),
            new("PENDING", "معلق", "Pending"),
            new("IN_PROGRESS", "قيد التنفيذ", "In Progress"),
            new("OUT_FOR_DELIVERY", "خرج للتوصيل", "Out for Delivery"),
            new("DELIVERED", "وصلنا", "Delivered"),
            new("COMPLETED", "مكتمل", "Completed"),
            new("CANCELLED", "ملغي", "Cancelled")
        ],
        PaymentStatuses:
        [
            new("PENDING", "بانتظار الدفع", "Awaiting Payment"),
            new("PAID", "مدفوع", "Paid"),
            new("FAILED", "فشل الدفع", "Payment Failed"),
            new("REFUNDED", "مسترجع", "Refunded"),
            new("PARTIALLY_REFUNDED", "استرجاع جزئي", "Partially Refunded"),
            new("COD_PENDING", "تحصيل عند التسليم", "Cash on Delivery"),
            new("SETTLED", "سوّينا", "Settled")
        ],
        FulfillmentStatuses:
        [
            new("QUEUED", "بانتظار التنفيذ", "Queued"),
            new("PREPARING", "قيد التجهيز", "Preparing"),
            new("READY_FOR_PICKUP", "جاهز للاستلام", "Ready for Pickup"),
            new("DRIVER_ASSIGNED", "عيّنا مندوب", "Driver Assigned"),
            new("PICKED_UP", "استلمنا", "Picked Up"),
            new("ON_ROUTE", "في الطريق", "On Route"),
            new("DELIVERED", "مسلّم", "Delivered"),
            new("FAILED", "فشل التنفيذ", "Fulfillment Failed"),
            new("CANCELLED", "أوقف التنفيذ", "Fulfillment Stopped")
        ],
        QueueViews:
        [
            new("ALL", "الكل", "All"),
            new("ACTIVE", "التشغيل المباشر", "Live Operations"),
            new("LATE", "متأخرة", "Delayed"),
            new("PAYMENT_ISSUES", "مشاكل دفع", "Payment Issues"),
            new("REFUNDS", "استرجاعات", "Refunds")
        ]);
}
