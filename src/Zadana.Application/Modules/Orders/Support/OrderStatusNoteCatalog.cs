namespace Zadana.Application.Modules.Orders.Support;

public record LocalizedText(string Ar, string En);

public static class OrderStatusNoteCatalog
{
    private static readonly Dictionary<string, LocalizedText> ExactNotes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Auto-dispatch started"] = new("بدأ البحث التلقائي عن مندوب", "Auto-dispatch started"),
        ["Auto-dispatch started."] = new("بدأ البحث التلقائي عن مندوب", "Auto-dispatch started"),
        ["Driver accepted delivery offer."] = new("وافق السائق على عرض التوصيل", "Driver accepted delivery offer"),
        ["Driver accepted delivery offer"] = new("وافق السائق على عرض التوصيل", "Driver accepted delivery offer"),
        ["Driver assigned via dispatch"] = new("تم تعيين السائق عبر التوجيه", "Driver assigned via dispatch"),
        ["Driver assigned via dispatch."] = new("تم تعيين السائق عبر التوجيه", "Driver assigned via dispatch"),
        ["Driver is on the way."] = new("المندوب في الطريق إليك", "Driver is on the way"),
        ["Driver is on the way"] = new("المندوب في الطريق إليك", "Driver is on the way"),
        ["Pickup confirmed before customer arrival."] = new("تم تأكيد الاستلام قبل الوصول للعميل", "Pickup confirmed before customer arrival"),
        ["Driver verified pickup OTP."] = new("تحقق السائق من رمز الاستلام", "Driver verified pickup OTP"),
        ["Driver verified pickup OTP"] = new("تحقق السائق من رمز الاستلام", "Driver verified pickup OTP"),
        ["Driver verified delivery OTP."] = new("تم التحقق من رمز التوصيل بنجاح", "Driver verified delivery OTP"),
        ["Driver verified delivery OTP"] = new("تم التحقق من رمز التوصيل بنجاح", "Driver verified delivery OTP"),
        ["Vendor confirmed pickup handoff via OTP."] = new("تم تأكيد تسليم الطلب للمندوب عبر الرمز", "Vendor confirmed pickup handoff via OTP"),
        ["Vendor confirmed pickup handoff via OTP"] = new("تم تأكيد تسليم الطلب للمندوب عبر الرمز", "Vendor confirmed pickup handoff via OTP"),
        ["Cash on delivery selected"] = new("تم اختيار الدفع عند الاستلام", "Cash on delivery selected"),
        ["Awaiting vendor response"] = new("في انتظار رد المتجر", "Awaiting vendor response"),
        ["Awaiting automatic bank transfer confirmation"] = new("بانتظار تأكيد التحويل البنكي التلقائي", "Awaiting automatic bank transfer confirmation"),
        ["Bank transfer proof uploaded"] = new("تم رفع إثبات التحويل البنكي", "Bank transfer proof uploaded"),
        ["Searching for drivers."] = new("جاري البحث عن سائقين", "Searching for drivers"),
        ["Searching for drivers"] = new("جاري البحث عن سائقين", "Searching for drivers"),
        ["No drivers available."] = new("لا يوجد سائقين متاحين", "No drivers available"),
        ["No drivers available"] = new("لا يوجد سائقين متاحين", "No drivers available"),
        ["Delivery offer sent"] = new("تم إرسال عرض التوصيل", "Delivery offer sent"),
        ["Driver rejected delivery offer."] = new("رفض السائق عرض التوصيل", "Driver rejected delivery offer"),
        ["Driver rejected delivery offer"] = new("رفض السائق عرض التوصيل", "Driver rejected delivery offer"),
        ["Cancelled by admin."] = new("تم الإلغاء من قبل الإدارة", "Cancelled by admin"),
        ["Cancelled by admin"] = new("تم الإلغاء من قبل الإدارة", "Cancelled by admin"),
        ["Cancelled by customer."] = new("تم الإلغاء من قبل العميل", "Cancelled by customer"),
        ["Cancelled by customer"] = new("تم الإلغاء من قبل العميل", "Cancelled by customer")
    };

    private static readonly Dictionary<string, LocalizedText> DispatchPendingCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["offer-timeout-exhausted"] = new(
            "التوجيه معلّق: انتهت مهلة عروض السائقين",
            "Dispatch pending: driver offer timeout exhausted"),
        ["missing-pickup-city"] = new(
            "التوجيه معلّق: مدينة الاستلام غير محددة",
            "Dispatch pending: pickup city is missing"),
        ["missing-customer-city"] = new(
            "التوجيه معلّق: مدينة العميل غير محددة",
            "Dispatch pending: customer city is missing"),
        ["pickup-customer-city-mismatch"] = new(
            "التوجيه معلّق: مدينة الاستلام تختلف عن مدينة العميل",
            "Dispatch pending: pickup and customer cities do not match"),
        ["no-eligible-driver-in-pickup-area"] = new(
            "التوجيه معلّق: لا يوجد سائق مؤهل في منطقة الاستلام",
            "Dispatch pending: no eligible driver in pickup area"),
        ["soft-blocked-by-rejections"] = new(
            "التوجيه معلّق: حظر مؤقت بسبب الرفض المتكرر",
            "Dispatch pending: temporarily blocked by repeated rejections"),
        ["no-eligible-driver"] = new(
            "التوجيه معلّق: لا يوجد سائق مؤهل",
            "Dispatch pending: no eligible driver")
    };

    public static LocalizedText SystemAutomaticUpdate { get; } =
        new("تحديث تلقائي للنظام", "System automatic update");

    public static LocalizedText LocalizeNote(string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return SystemAutomaticUpdate;
        }

        var trimmed = note.Trim();

        if (ExactNotes.TryGetValue(trimmed, out var exact))
        {
            return exact;
        }

        if (TryLocalizeDispatchPending(trimmed, out var dispatchPending))
        {
            return dispatchPending;
        }

        if (trimmed.StartsWith("Driver assigned by admin", StringComparison.OrdinalIgnoreCase))
        {
            return new("تم تعيين السائق من قبل الإدارة", trimmed.TrimEnd('.'));
        }

        if (trimmed.StartsWith("Bank transfer confirmed by", StringComparison.OrdinalIgnoreCase))
        {
            return new("تم تأكيد التحويل البنكي", trimmed);
        }

        if (trimmed.StartsWith("Customer cancellation reason:", StringComparison.OrdinalIgnoreCase))
        {
            return new(trimmed, trimmed);
        }

        return new(trimmed, trimmed);
    }

    public static LocalizedText LocalizeDispatchPendingCode(string code)
    {
        var normalized = code.Trim();
        return DispatchPendingCodes.TryGetValue(normalized, out var localized)
            ? localized
            : new($"التوجيه معلّق: {normalized}", $"Dispatch pending: {normalized}");
    }

    private static bool TryLocalizeDispatchPending(string note, out LocalizedText localized)
    {
        const string prefix = "Dispatch pending:";
        if (!note.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            localized = default!;
            return false;
        }

        var code = note[prefix.Length..].Trim();
        localized = LocalizeDispatchPendingCode(code);
        return true;
    }
}
