using System.Text.Json;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Social.Enums;

namespace Zadana.Application.Modules.Orders.Support;

internal static class OrderSupportCaseNotificationComposer
{
    public static CustomerOrderSupportCaseNotification ComposeCustomer(
        Guid orderId,
        Guid caseId,
        string orderNumber,
        OrderSupportCaseType type,
        OrderSupportCaseStatus status,
        string action)
    {
        var targetUrl = ResolveTargetUrl(orderId, caseId);
        var typeValue = NotificationTypes.OrderSupportCaseChanged;
        var (titleAr, titleEn, bodyAr, bodyEn) = GetCustomerNotificationContent(orderNumber, type, status, action);

        return new CustomerOrderSupportCaseNotification(
            titleAr,
            titleEn,
            bodyAr,
            bodyEn,
            typeValue,
            BuildData(orderId, caseId, orderNumber, type, status, action, targetUrl),
            action,
            targetUrl);
    }

    public static string BuildData(
        Guid orderId,
        Guid caseId,
        string orderNumber,
        OrderSupportCaseType type,
        OrderSupportCaseStatus status,
        string action,
        string targetUrl) =>
        JsonSerializer.Serialize(new
        {
            orderId,
            caseId,
            orderNumber,
            type = ToApiValue(type),
            status = ToApiValue(status),
            action,
            targetUrl
        });

    public static string ResolveTargetUrl(Guid orderId, Guid caseId) => $"/orders/{orderId}/cases/{caseId}";

    public static string ToApiValue(OrderSupportCaseType type) =>
        type switch
        {
            OrderSupportCaseType.ReturnRequest => "return_request",
            _ => "complaint"
        };

    public static string ToApiValue(OrderSupportCaseStatus status) =>
        status switch
        {
            OrderSupportCaseStatus.InReview => "in_review",
            OrderSupportCaseStatus.AwaitingCustomerEvidence => "awaiting_customer_evidence",
            _ => status.ToString().ToLowerInvariant()
        };

    private static (string TitleAr, string TitleEn, string BodyAr, string BodyEn) GetCustomerNotificationContent(
        string orderNumber,
        OrderSupportCaseType type,
        OrderSupportCaseStatus status,
        string action)
    {
        return (type, status, action) switch
        {
            (_, OrderSupportCaseStatus.AwaitingCustomerEvidence, _) => (
                "مطلوب مستندات إضافية",
                "More evidence is required",
                $"نحتاج معلومات أو أدلة إضافية لمتابعة الحالة المرتبطة بطلب {orderNumber}.",
                $"We need additional information or evidence to continue reviewing the case for order #{orderNumber}."),
            (_, OrderSupportCaseStatus.Approved, _) => (
                "تمت الموافقة على الحالة",
                "Case approved",
                $"تمت الموافقة على الحالة المرتبطة بطلب {orderNumber}.",
                $"The case linked to order #{orderNumber} has been approved."),
            (_, OrderSupportCaseStatus.Rejected, _) => (
                "تم رفض الحالة",
                "Case rejected",
                $"تم رفض الحالة المرتبطة بطلب {orderNumber}.",
                $"The case linked to order #{orderNumber} has been rejected."),
            (_, OrderSupportCaseStatus.Resolved, _) => (
                "تم إغلاق الحالة",
                "Case resolved",
                $"تم إغلاق الحالة المرتبطة بطلب {orderNumber}.",
                $"The case linked to order #{orderNumber} has been resolved."),
            (OrderSupportCaseType.ReturnRequest, _, "created") => (
                "تم استلام طلب الاسترجاع",
                "Return request received",
                $"استلمنا طلب الاسترجاع الخاص بطلب {orderNumber} وسيتم مراجعته.",
                $"We received your return request for order #{orderNumber} and it is now under review."),
            _ => (
                "تم تحديث حالة الشكوى",
                "Support case updated",
                $"تم تحديث الحالة المرتبطة بطلب {orderNumber}.",
                $"The support case linked to order #{orderNumber} has been updated.")
        };
    }
}

internal sealed record CustomerOrderSupportCaseNotification(
    string TitleAr,
    string TitleEn,
    string BodyAr,
    string BodyEn,
    string NotificationType,
    string Data,
    string Action,
    string TargetUrl);
