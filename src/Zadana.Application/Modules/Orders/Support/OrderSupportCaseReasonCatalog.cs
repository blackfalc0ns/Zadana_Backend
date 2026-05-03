namespace Zadana.Application.Modules.Orders.Support;

public static class OrderSupportCaseReasonCatalog
{
    private static readonly IReadOnlyList<OrderSupportCaseReasonOption> DriverReportOptions =
    [
        new("wrong_address", "العنوان خاطئ", "Wrong address", false),
        new("customer_unavailable", "العميل غير متوفر", "Customer unavailable", false),
        new("damaged_package", "الشحنة تالفة", "Damaged package", true),
        new("vehicle_issue", "عطل في المركبة", "Vehicle issue", true),
        new("other", "أخرى", "Other", true)
    ];

    private static readonly IReadOnlyList<OrderSupportCaseReasonOption> DriverDisputeOptions =
    [
        new("payout_dispute", "نزاع مالي", "Payout dispute", false),
        new("incorrect_deduction", "خصم غير صحيح", "Incorrect deduction", false),
        new("other", "أخرى", "Other", true)
    ];

    private static readonly IReadOnlyList<OrderSupportCaseReasonOption> CustomerComplaintOptions =
    [
        new("late_delivery", "تأخر التوصيل", "Late delivery", false),
        new("missing_items", "عناصر مفقودة", "Missing items", false),
        new("wrong_items", "عناصر خاطئة", "Wrong items", false),
        new("poor_quality", "جودة سيئة", "Poor quality", true),
        new("driver_behavior", "سلوك المندوب", "Driver behavior", true),
        new("other", "أخرى", "Other", true)
    ];

    private static readonly IReadOnlyList<OrderSupportCaseReasonOption> CustomerReturnOptions =
    [
        new("defective", "منتج معيب", "Defective product", true),
        new("not_as_described", "غير مطابق للوصف", "Not as described", true),
        new("wrong_size", "المقاس خاطئ", "Wrong size", false),
        new("changed_mind", "غيرت رأيي", "Changed mind", false),
        new("other", "أخرى", "Other", true)
    ];

    public static IReadOnlyList<OrderSupportCaseReasonOption> GetReasonsByType(string type)
    {
        return type.Trim().ToLowerInvariant() switch
        {
            "driver_report" or "report" => DriverReportOptions,
            "driver_dispute" or "dispute" => DriverDisputeOptions,
            "complaint" => CustomerComplaintOptions,
            "return" or "return_request" => CustomerReturnOptions,
            _ => []
        };
    }
}

public sealed record OrderSupportCaseReasonOption(
    string Code,
    string LabelAr,
    string LabelEn,
    bool RequiresNote);
