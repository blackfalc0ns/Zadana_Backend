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
        string targetUrl)
    {
        var typeValue = ToApiValue(type);
        var statusValue = ToApiValue(status);
        var isReturnRequest = type == OrderSupportCaseType.ReturnRequest;
        var popupType = isReturnRequest
            ? "return_request_status_update"
            : "support_case_status_update";
        var eventName = isReturnRequest
            ? $"return.{action}"
            : $"support.{action}";

        var data = new Dictionary<string, object?>
        {
            ["orderId"] = orderId,
            ["caseId"] = caseId,
            ["orderNumber"] = orderNumber,
            ["type"] = typeValue,
            ["status"] = statusValue,
            ["action"] = action,
            ["targetUrl"] = targetUrl,
            ["category"] = "support",
            ["screen"] = "support_case_detail",
            ["presentation"] = "popup",
            ["popupType"] = popupType,
            ["showPopup"] = true,
            ["eventName"] = eventName
        };

        if (isReturnRequest)
        {
            data["isReturnRequest"] = true;
            data["returnStatus"] = statusValue;
            data["refundStatus"] = ResolveReturnRefundStatus(status, action);
            data["returnPopupType"] = popupType;
        }

        return JsonSerializer.Serialize(data);
    }

    private static string ResolveReturnRefundStatus(OrderSupportCaseStatus status, string action) =>
        status switch
        {
            OrderSupportCaseStatus.Approved => "approved",
            OrderSupportCaseStatus.Rejected => "rejected",
            OrderSupportCaseStatus.Resolved => "resolved",
            OrderSupportCaseStatus.InReview => "in_review",
            OrderSupportCaseStatus.AwaitingCustomerEvidence => "awaiting_customer_evidence",
            _ => action
        };

    public static string ResolveTargetUrl(Guid orderId, Guid caseId) => $"/orders/{orderId}/cases/{caseId}";
    public static string ResolveAdminTargetUrl(Guid caseId, OrderSupportCaseType type) =>
        type switch
        {
            OrderSupportCaseType.Complaint => $"/support?tab=legacy&legacyCaseId={caseId}",
            OrderSupportCaseType.ReturnRequest => $"/finances/refunds?focus={caseId}",
            _ => $"/disputes?focus={caseId}"
        };

    public static string ToApiValue(OrderSupportCaseType type) =>
        type switch
        {
            OrderSupportCaseType.ReturnRequest => "return_request",
            OrderSupportCaseType.DriverReport => "driver_report",
            OrderSupportCaseType.DriverDispute => "driver_dispute",
            OrderSupportCaseType.DriverAccountAppeal => "driver_account",
            _ => "complaint"
        };

    public static string ToApiValue(OrderSupportCaseStatus status) =>
        status switch
        {
            OrderSupportCaseStatus.InReview => "in_review",
            OrderSupportCaseStatus.AwaitingCustomerEvidence => "awaiting_customer_evidence",
            _ => status.ToString().ToLowerInvariant()
        };

    public static InternalOrderSupportCaseNotification ComposeAdmin(
        Guid orderId,
        Guid caseId,
        string orderNumber,
        OrderSupportCaseType type,
        OrderSupportCaseStatus status,
        OrderSupportCaseQueue queue,
        OrderSupportCasePriority priority,
        string action)
    {
        var targetUrl = ResolveAdminTargetUrl(caseId, type);
        var notificationType = action switch
        {
            "created" => NotificationTypes.AdminOrderSupportCaseCreated,
            "assigned" => NotificationTypes.AdminOrderSupportCaseAssigned,
            _ => NotificationTypes.AdminOrderSupportCaseEscalated
        };

        var queueLabelEn = queue.ToString();
        var queueLabelAr = queue switch
        {
            OrderSupportCaseQueue.Support => "الدعم",
            OrderSupportCaseQueue.Finance => "المالية",
            OrderSupportCaseQueue.Operations => "العمليات",
            OrderSupportCaseQueue.Risk => "المخاطر",
            OrderSupportCaseQueue.Legal => "الشؤون القانونية",
            OrderSupportCaseQueue.DriverOps => "عمليات المندوبين",
            _ => queue.ToString()
        };
        var priorityLabelEn = priority.ToString().ToLowerInvariant();
        var priorityLabelAr = priority switch
        {
            OrderSupportCasePriority.Low => "منخفضة",
            OrderSupportCasePriority.Medium => "متوسطة",
            OrderSupportCasePriority.High => "عالية",
            OrderSupportCasePriority.Critical => "حرجة",
            _ => priority.ToString()
        };
        var typeLabelEn = type switch
        {
            OrderSupportCaseType.ReturnRequest => "return request",
            OrderSupportCaseType.Complaint => "support case",
            OrderSupportCaseType.DriverReport => "driver report",
            OrderSupportCaseType.DriverDispute => "driver dispute",
            _ => "support case"
        };
        var typeLabelAr = type switch
        {
            OrderSupportCaseType.ReturnRequest => "طلب استرجاع",
            OrderSupportCaseType.Complaint => "حالة دعم",
            OrderSupportCaseType.DriverReport => "بلاغ مندوب",
            OrderSupportCaseType.DriverDispute => "اعتراض مندوب",
            OrderSupportCaseType.DriverAccountAppeal => "اعتراض حساب مندوب",
            _ => "حالة دعم"
        };

        var (titleAr, titleEn, bodyAr, bodyEn) = action switch
        {
            "created" => (
                "حالة دعم جديدة تحتاج مراجعة",
                "New support case requires review",
                $"فتحنا {typeLabelAr} جديدة للطلب رقم {orderNumber} ووجهناها لفريق {queueLabelAr}.",
                $"A new {typeLabelEn} was created for order #{orderNumber} and routed to the {queueLabelEn} queue."),
            "assigned" => (
                "أسندنا لك حالة دعم",
                "A support case was assigned to you",
                $"أسندنا لك الحالة المرتبطة بالطلب رقم {orderNumber} للمتابعة.",
                $"The support case linked to order #{orderNumber} has been assigned to you for follow-up."),
            _ => (
                "صعّدنا حالة الدعم",
                "Support case escalated",
                $"صعّدنا الحالة المرتبطة بالطلب رقم {orderNumber} لفريق {queueLabelAr} بأولوية {priorityLabelAr}.",
                $"The support case linked to order #{orderNumber} was escalated to the {queueLabelEn} queue with {priorityLabelEn} priority.")
        };

        var data = JsonSerializer.Serialize(new
        {
            orderId,
            caseId,
            orderNumber,
            type = ToApiValue(type),
            status = ToApiValue(status),
            queue = queue.ToString().ToLowerInvariant(),
            priority = priorityLabelEn,
            action,
            targetUrl
        });

        return new InternalOrderSupportCaseNotification(
            titleAr,
            titleEn,
            bodyAr,
            bodyEn,
            notificationType,
            data,
            action,
            targetUrl);
    }

    private static (string TitleAr, string TitleEn, string BodyAr, string BodyEn) GetCustomerNotificationContent(
        string orderNumber,
        OrderSupportCaseType type,
        OrderSupportCaseStatus status,
        string action)
    {
        return (type, status, action) switch
        {
            (_, OrderSupportCaseStatus.InReview, "assigned") => (
                "بدأت مراجعة الحالة",
                "Case under review",
                $"بدأ فريق الدعم مراجعة الحالة المرتبطة بطلب {orderNumber}.",
                $"Support has started reviewing the case for order #{orderNumber}."),
            (_, OrderSupportCaseStatus.InReview, "escalated") => (
                "صعّدنا الحالة",
                "Case escalated",
                $"صعّدنا الحالة المرتبطة بطلب {orderNumber} للمراجعة.",
                $"The case linked to order #{orderNumber} was escalated for review."),
            (_, _, "admin_message") => (
                "رسالة جديدة من الدعم",
                "New support message",
                $"وصلتك رسالة جديدة من الدعم بخصوص طلب {orderNumber}.",
                $"There is a new support message about order #{orderNumber}."),
            (_, _, "note_added") => (
                "أضفنا ملاحظة",
                "New support note",
                $"أضفنا ملاحظة على الحالة المرتبطة بطلب {orderNumber}.",
                $"A note was added to the case for order #{orderNumber}."),
            (_, OrderSupportCaseStatus.AwaitingCustomerEvidence, _) => (
                "مطلوب مستندات إضافية",
                "More evidence is required",
                $"نحتاج معلومات أو أدلة إضافية لمتابعة الحالة المرتبطة بطلب {orderNumber}.",
                $"We need additional information or evidence to continue reviewing the case for order #{orderNumber}."),
            (_, OrderSupportCaseStatus.Approved, _) => (
                "اعتمدنا الحالة",
                "Case approved",
                $"اعتمدنا الحالة المرتبطة بطلب {orderNumber}.",
                $"The case linked to order #{orderNumber} has been approved."),
            (_, OrderSupportCaseStatus.Rejected, _) => (
                "رفضنا الحالة",
                "Case rejected",
                $"رفضنا الحالة المرتبطة بطلب {orderNumber}.",
                $"The case linked to order #{orderNumber} has been rejected."),
            (_, OrderSupportCaseStatus.Resolved, _) => (
                "أغلقنا الحالة",
                "Case resolved",
                $"أغلقنا الحالة المرتبطة بطلب {orderNumber}.",
                $"The case linked to order #{orderNumber} has been resolved."),
            (OrderSupportCaseType.ReturnRequest, _, "created") => (
                "استلمنا طلب الاسترجاع",
                "Return request received",
                $"استلمنا طلب الاسترجاع الخاص بطلب {orderNumber} وبنراجعه.",
                $"We received your return request for order #{orderNumber} and it is now under review."),
            _ => (
                "حدّثنا حالة الشكوى",
                "Support case updated",
                $"حدّثنا الحالة المرتبطة بطلب {orderNumber}.",
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

internal sealed record InternalOrderSupportCaseNotification(
    string TitleAr,
    string TitleEn,
    string BodyAr,
    string BodyEn,
    string NotificationType,
    string Data,
    string Action,
    string TargetUrl);
