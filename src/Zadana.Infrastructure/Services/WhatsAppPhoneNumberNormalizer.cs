using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Zadana.Infrastructure.Services;

public static partial class WhatsAppPhoneNumberNormalizer
{
    public static string Normalize(string phoneNumber, string defaultCountryCode)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            throw new ArgumentException("Phone number is required.", nameof(phoneNumber));
        }

        var normalizedCountryCode = NormalizeCountryCode(defaultCountryCode);
        var compact = CompactPhoneNumber(phoneNumber);

        if (compact.StartsWith('0'))
        {
            compact = normalizedCountryCode + compact[1..];
        }
        else if (!compact.StartsWith('+'))
        {
            compact = normalizedCountryCode + compact;
        }

        if (!E164PhoneRegex().IsMatch(compact))
        {
            throw new ArgumentException("Phone number must be in a deliverable international format.", nameof(phoneNumber));
        }

        return compact;
    }

    public static string NormalizeCountryCode(string defaultCountryCode)
    {
        if (string.IsNullOrWhiteSpace(defaultCountryCode))
        {
            throw new ArgumentException("Default country code is required.", nameof(defaultCountryCode));
        }

        var compact = CompactPhoneNumber(defaultCountryCode);
        if (!compact.StartsWith('+'))
        {
            compact = "+" + compact;
        }

        if (!CountryCodeRegex().IsMatch(compact))
        {
            throw new ArgumentException("Default country code must be an international dialing code.", nameof(defaultCountryCode));
        }

        return compact;
    }

    private static string CompactPhoneNumber(string value)
    {
        var builder = new StringBuilder(value.Length);

        foreach (var ch in value.Trim())
        {
            if (char.IsWhiteSpace(ch) || ch is '-' or '(' or ')')
            {
                continue;
            }

            if (ch == '+')
            {
                builder.Append(ch);
                continue;
            }

            var numericValue = char.GetNumericValue(ch);
            if (numericValue >= 0 && numericValue <= 9 && numericValue == Math.Truncate(numericValue))
            {
                builder.Append(((int)numericValue).ToString(CultureInfo.InvariantCulture));
                continue;
            }

            builder.Append(ch);
        }

        return builder.ToString();
    }

    [GeneratedRegex(@"^\+[1-9]\d{7,14}$")]
    private static partial Regex E164PhoneRegex();

    [GeneratedRegex(@"^\+[1-9]\d{0,3}$")]
    private static partial Regex CountryCodeRegex();
}
