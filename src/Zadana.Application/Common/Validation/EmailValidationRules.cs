using System.Net.Mail;

namespace Zadana.Application.Common.Validation;

public static class EmailValidationRules
{
    public static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var normalizedEmail = email.Trim();
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
