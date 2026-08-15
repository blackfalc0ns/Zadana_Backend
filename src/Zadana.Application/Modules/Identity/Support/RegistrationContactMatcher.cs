using Zadana.Domain.Modules.Identity.Support;

namespace Zadana.Application.Modules.Identity.Support;

public static class RegistrationContactMatcher
{
    public static bool Matches(string identifier, string? email, string? phone)
    {
        var trimmed = identifier.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(email) &&
            string.Equals(email.Trim(), trimmed, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return PhonesMatch(trimmed, phone);
    }

    public static bool PhonesMatch(string? left, string? right)
    {
        var leftDigits = DigitsOnly(left);
        var rightDigits = DigitsOnly(right);
        if (leftDigits.Length < 8 || rightDigits.Length < 8)
        {
            return false;
        }

        if (leftDigits == rightDigits)
        {
            return true;
        }

        var length = Math.Min(9, Math.Min(leftDigits.Length, rightDigits.Length));
        return leftDigits[^length..] == rightDigits[^length..];
    }

    public static string DigitsOnly(string? value) => OtpCodeNormalizer.Normalize(value);
}
