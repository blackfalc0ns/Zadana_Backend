using System.Net.Mail;

namespace Zadana.Application.Common.Validation;

public static class EmailValidationRules
{
    public static bool HasComTopLevelDomain(string? email) =>
        !string.IsNullOrWhiteSpace(email) &&
        email.Trim().EndsWith(".com", StringComparison.OrdinalIgnoreCase);

    public static bool IsValidComEmail(string? email)
    {
        if (!HasComTopLevelDomain(email))
        {
            return false;
        }

        var normalizedEmail = email!.Trim();
        try
        {
            var address = new MailAddress(normalizedEmail);
            return string.Equals(address.Address, normalizedEmail, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
