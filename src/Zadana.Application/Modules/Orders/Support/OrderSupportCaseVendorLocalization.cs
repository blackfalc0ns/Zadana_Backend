using System.Globalization;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;

namespace Zadana.Application.Modules.Orders.Support;

public static class OrderSupportCaseVendorLocalization
{
    public static bool IsArabicRequest() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase);

    public static string Pick(string ar, string en, bool isArabic) => isArabic ? ar : en;

    public static string? ResolveReasonLabel(OrderSupportCaseType type, string? reasonCode, bool isArabic)
    {
        var reason = OrderSupportCaseReasonCatalog.FindReason(MapSupportCaseType(type), reasonCode);
        return reason is null ? reasonCode : Pick(reason.LabelAr, reason.LabelEn, isArabic);
    }

    public static string ResolveQueueLabel(OrderSupportCaseQueue queue, bool isArabic) =>
        queue switch
        {
            OrderSupportCaseQueue.Support => Pick("دعم العملاء", "Customer support", isArabic),
            OrderSupportCaseQueue.Finance => Pick("المالية", "Finance", isArabic),
            OrderSupportCaseQueue.Operations => Pick("التشغيل", "Operations", isArabic),
            OrderSupportCaseQueue.Risk => Pick("المخاطر", "Risk", isArabic),
            OrderSupportCaseQueue.Legal => Pick("القانونية", "Legal", isArabic),
            OrderSupportCaseQueue.DriverOps => Pick("عمليات المناديب", "Driver operations", isArabic),
            _ => queue.ToString()
        };

    public static string? ResolveRefundMethod(string? refundMethod, bool isArabic) =>
        (refundMethod ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "wallet" => Pick("المحفظة", "Wallet", isArabic),
            "bank" => Pick("تحويل بنكي", "Bank transfer", isArabic),
            "card" => Pick("البطاقة", "Card", isArabic),
            "cash" => Pick("نقدًا", "Cash", isArabic),
            "coupon" => Pick("كوبون", "Coupon", isArabic),
            "" => null,
            _ => refundMethod
        };

    public static string? ResolveCostBearer(string? costBearer, bool isArabic) =>
        (costBearer ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "vendor" => Pick("التاجر", "Vendor", isArabic),
            "platform" => Pick("المنصة", "Platform", isArabic),
            "driver" => Pick("المندوب", "Driver", isArabic),
            "customer" => Pick("العميل", "Customer", isArabic),
            "" => null,
            _ => costBearer
        };

    public static string ResolveActivityTitle(OrderSupportCaseActivity activity, bool isArabic) =>
        NormalizeAction(activity.Action) switch
        {
            "submitted" => Pick("فتحنا الحالة", "Case opened", isArabic),
            "driver_response" => Pick("رد المندوب", "Driver replied", isArabic),
            "vendor_response" => Pick("رد التاجر", "Vendor replied", isArabic),
            "customer_response" => Pick("رد العميل", "Customer replied", isArabic),
            "request_evidence" => Pick("طلب معلومات إضافية", "More evidence requested", isArabic),
            "assigned" => Pick("أسندنا", "Case assigned", isArabic),
            "escalated" => Pick("صعّدنا", "Case escalated", isArabic),
            "approved" => Pick("اعتمدنا", "Case approved", isArabic),
            "rejected" => Pick("رفضنا", "Case rejected", isArabic),
            "resolved" => Pick("حلّينا", "Case resolved", isArabic),
            "reopened" => Pick("أعيد فتح الحالة", "Case reopened", isArabic),
            "admin_message" => Pick("رسالة من الإدارة", "Admin update", isArabic),
            "internal_note" => Pick("ملاحظة داخلية", "Internal note", isArabic),
            "customer_note" => Pick("ملاحظة عامة", "Public note", isArabic),
            _ => string.IsNullOrWhiteSpace(activity.Title) ? activity.Action : activity.Title
        };

    public static string? ResolveActivityNote(
        OrderSupportCase supportCase,
        OrderSupportCaseActivity activity,
        bool couponRedeemed,
        bool isArabic)
    {
        var orderNumber = supportCase.Order?.OrderNumber ?? supportCase.OrderId?.ToString() ?? string.Empty;
        var templated = NormalizeAction(activity.Action) switch
        {
            "submitted" => supportCase.Type == OrderSupportCaseType.ReturnRequest
                ? Pick(
                    $"استلمنا طلب الاسترجاع للطلب رقم {orderNumber} وهو الآن تحت المراجعة.",
                    $"We received the return request for order #{orderNumber} and it is now under review.",
                    isArabic)
                : Pick(
                    $"استلمنا الحالة المرتبطة بالطلب رقم {orderNumber} وهي الآن تحت المراجعة.",
                    $"We received the support case for order #{orderNumber} and it is now under review.",
                    isArabic),
            "request_evidence" => Pick(
                $"نحتاج إلى معلومات أو أدلة إضافية لمتابعة مراجعة الحالة الخاصة بالطلب رقم {orderNumber}.",
                $"We need additional information or evidence to continue reviewing the case for order #{orderNumber}.",
                isArabic),
            "approved" => supportCase.Type == OrderSupportCaseType.ReturnRequest
                ? Pick(
                    "اعتمدنا طلب الاسترجاع وراح نبلغك عند بدء المعالجة المالية.",
                    "Your return request has been approved. You will be notified when the financial processing begins.",
                    isArabic)
                : Pick(
                    $"اعتمدنا الحالة الخاصة بالطلب رقم {orderNumber}.",
                    $"The support case linked to order #{orderNumber} has been approved.",
                    isArabic),
            "rejected" => Pick(
                $"رفضنا الحالة الخاصة بالطلب رقم {orderNumber}.",
                $"The support case linked to order #{orderNumber} has been rejected.",
                isArabic),
            "resolved" => Pick(
                $"أغلقنا الحالة الخاصة بالطلب رقم {orderNumber} بعد معالجتها.",
                $"The support case linked to order #{orderNumber} has been resolved and closed.",
                isArabic),
            "reopened" => Pick(
                $"أعيد فتح الحالة الخاصة بالطلب رقم {orderNumber} لمراجعتها مرة أخرى.",
                $"The support case linked to order #{orderNumber} was reopened for another review.",
                isArabic),
            "escalated" => Pick(
                $"صعّدنا الحالة الخاصة بالطلب رقم {orderNumber} إلى فريق مختص.",
                $"The support case linked to order #{orderNumber} was escalated to a specialized team.",
                isArabic),
            "admin_message" => Pick(
                "شارك فريق الدعم تحديثًا على هذه الحالة.",
                "Support shared an update on this case.",
                isArabic),
            _ => null
        };

        if (!string.IsNullOrWhiteSpace(templated))
        {
            return templated;
        }

        return string.IsNullOrWhiteSpace(activity.Note) ? null : activity.Note.Trim();
    }

    public static string? ResolveCustomerVisibleNote(
        OrderSupportCase supportCase,
        bool couponRedeemed,
        bool isArabic)
    {
        if (!string.IsNullOrWhiteSpace(supportCase.CustomerVisibleNote))
        {
            return supportCase.CustomerVisibleNote.Trim();
        }

        return supportCase.Status switch
        {
            OrderSupportCaseStatus.Approved => Pick(
                "اعتمدنا طلب الاسترجاع وراح نبلغك عند بدء المعالجة المالية.",
                "Your return request has been approved. You will be notified when the financial processing begins.",
                isArabic),
            OrderSupportCaseStatus.Rejected => Pick(
                "رفضنا طلب الاسترجاع بعد المراجعة.",
                "Your return request was rejected after review.",
                isArabic),
            OrderSupportCaseStatus.Resolved when supportCase.CompensationType == OrderSupportCaseCompensationType.CashRefund => Pick(
                "أكملنا الاسترجاع وأغلقنا الحالة.",
                "Your refund has been completed and the case is now closed.",
                isArabic),
            OrderSupportCaseStatus.Resolved when supportCase.CompensationType == OrderSupportCaseCompensationType.CouponCompensation && couponRedeemed => Pick(
                "استخدمنا كوبون التعويض وأغلقنا الحالة.",
                "Your compensation coupon was redeemed and the case is now closed.",
                isArabic),
            OrderSupportCaseStatus.Resolved when supportCase.CompensationType == OrderSupportCaseCompensationType.CouponCompensation => Pick(
                "أصدرنا كوبون التعويض وأغلقنا الحالة.",
                "A compensation coupon was issued and the case is now closed.",
                isArabic),
            OrderSupportCaseStatus.AwaitingCustomerEvidence => Pick(
                "نحتاج إلى مزيد من المعلومات لمتابعة مراجعة هذه الحالة.",
                "More information is required to continue reviewing this case.",
                isArabic),
            _ => null
        };
    }

    public static string? ResolveDecisionNotes(
        OrderSupportCase supportCase,
        bool couponRedeemed,
        bool isArabic)
    {
        if (!string.IsNullOrWhiteSpace(supportCase.DecisionNotes))
        {
            return supportCase.DecisionNotes.Trim();
        }

        return supportCase.Status switch
        {
            OrderSupportCaseStatus.Approved when supportCase.Type == OrderSupportCaseType.ReturnRequest => Pick(
                "الطلب مطابق لسياسة المنصة واعتمدنا عليه بعد مراجعة الأدلة.",
                "The request matches platform policy and was approved after evidence review.",
                isArabic),
            OrderSupportCaseStatus.Rejected => Pick(
                "الطلب لم يستوفِ معايير الاعتماد بعد مراجعة الأدلة.",
                "The request did not meet the approval criteria after evidence review.",
                isArabic),
            OrderSupportCaseStatus.Resolved when supportCase.CompensationType == OrderSupportCaseCompensationType.CashRefund => Pick(
                "أُغلقت الحالة بعد إكمال استرجاع العميل.",
                "The case was closed after completing the customer refund.",
                isArabic),
            OrderSupportCaseStatus.Resolved when supportCase.CompensationType == OrderSupportCaseCompensationType.CouponCompensation && couponRedeemed => Pick(
                "أُغلقت الحالة بعد استخدام كوبون التعويض.",
                "The case was closed after the compensation coupon was redeemed.",
                isArabic),
            OrderSupportCaseStatus.Resolved when supportCase.CompensationType == OrderSupportCaseCompensationType.CouponCompensation => Pick(
                "أُغلقت الحالة بعد إصدار كوبون التعويض.",
                "The case was closed after issuing the compensation coupon.",
                isArabic),
            OrderSupportCaseStatus.AwaitingCustomerEvidence => Pick(
                "نحتاج إلى أدلة إضافية قبل اتخاذ قرار نهائي.",
                "Additional evidence is required before a final decision can be made.",
                isArabic),
            _ => null
        };
    }

    private static string MapSupportCaseType(OrderSupportCaseType type) =>
        type switch
        {
            OrderSupportCaseType.ReturnRequest => "return_request",
            OrderSupportCaseType.DriverReport => "driver_report",
            OrderSupportCaseType.DriverDispute => "driver_dispute",
            OrderSupportCaseType.DriverAccountAppeal => "driver_account",
            _ => "complaint"
        };

    private static string NormalizeAction(string? action) =>
        string.IsNullOrWhiteSpace(action) ? string.Empty : action.Trim().ToLowerInvariant();
}
