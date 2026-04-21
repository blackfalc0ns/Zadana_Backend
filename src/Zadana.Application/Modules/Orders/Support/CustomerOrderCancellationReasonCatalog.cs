namespace Zadana.Application.Modules.Orders.Support;

public static class CustomerOrderCancellationReasonCatalog
{
    private static readonly IReadOnlyList<CustomerOrderCancellationReasonOption> Options =
    [
        new("changed_my_mind", "غيرت رأيي", "Changed my mind", false),
        new("ordered_by_mistake", "طلبت بالخطأ", "Ordered by mistake", false),
        new("price_too_high", "السعر مرتفع", "Price is too high", false),
        new("want_to_modify_order", "أريد تعديل الطلب", "I want to modify the order", false),
        new("address_not_suitable", "العنوان غير مناسب", "Address is not suitable", false),
        new("other", "أخرى", "Other", true)
    ];

    public static IReadOnlyList<CustomerOrderCancellationReasonOption> GetAll() => Options;

    public static bool IsValidCode(string? code) =>
        !string.IsNullOrWhiteSpace(code) &&
        Options.Any(x => x.Code.Equals(code.Trim(), StringComparison.OrdinalIgnoreCase));

    public static CustomerOrderCancellationReasonOption? FindByCode(string? code) =>
        string.IsNullOrWhiteSpace(code)
            ? null
            : Options.FirstOrDefault(x => x.Code.Equals(code.Trim(), StringComparison.OrdinalIgnoreCase));
}

public sealed record CustomerOrderCancellationReasonOption(
    string Code,
    string LabelAr,
    string LabelEn,
    bool RequiresNote);
