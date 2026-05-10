using Zadana.Domain.Modules.Identity.Entities;

namespace Zadana.Application.Modules.Payments.Support;

internal static class PaymobBillingData
{
    public static string FirstName(User user)
    {
        var parts = SplitName(user.FullName);
        return FirstNonBlank(parts.FirstOrDefault(), user.FullName, "Customer");
    }

    public static string LastName(User user)
    {
        var parts = SplitName(user.FullName);
        return FirstNonBlank(
            parts.Length > 1 ? string.Join(' ', parts.Skip(1)) : null,
            parts.FirstOrDefault(),
            "Customer");
    }

    public static string Email(User user) =>
        FirstNonBlank(user.Email, user.UserName, "customer@zadana.local");

    public static string Phone(User user, CustomerAddress address) =>
        FirstNonBlank(user.PhoneNumber, address.ContactPhone, "01000000000");

    public static string Street(CustomerAddress address) =>
        FirstNonBlank(address.AddressLine, address.Area, address.City, "NA");

    public static string City(CustomerAddress address) =>
        FirstNonBlank(address.City, address.Area, "Cairo");

    private static string[] SplitName(string? fullName) =>
        string.IsNullOrWhiteSpace(fullName)
            ? []
            : fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string FirstNonBlank(params string?[] values) =>
        values.First(value => !string.IsNullOrWhiteSpace(value))!.Trim();
}
