namespace Zadana.Application.Modules.Identity.DTOs;

public record AdminCustomerFilterOptionDto(string Value, string LabelAr, string LabelEn);

public record AdminCustomerFilterOptionsDto(
    IReadOnlyList<AdminCustomerFilterOptionDto> Statuses,
    IReadOnlyList<AdminCustomerFilterOptionDto> Cities);

public static class AdminCustomerFilterOptionsFactory
{
    public static IReadOnlyList<AdminCustomerFilterOptionDto> BuildStatuses() =>
    [
        new("Active", "نشط", "Active"),
        new("Suspended", "مقيد", "Suspended"),
        new("Banned", "محظور", "Banned"),
        new("Inactive", "غير نشط", "Inactive"),
        new("Pending", "بانتظار المعالجة", "Pending")
    ];
}
