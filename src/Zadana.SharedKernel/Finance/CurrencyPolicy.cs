using Zadana.SharedKernel.Exceptions;

namespace Zadana.SharedKernel.Finance;

/// <summary>
/// Single source of truth for the platform's official currency policy.
/// Per the revised SAR-only financial workflow, every financial entity, DTO,
/// payment, settlement, and payout MUST use SAR. Any other currency is rejected.
/// </summary>
public static class CurrencyPolicy
{
    /// <summary>
    /// The only currency accepted by the system going forward.
    /// </summary>
    public const string OfficialCurrency = "SAR";

    /// <summary>
    /// Minor unit divisor for SAR (1 SAR = 100 halalas).
    /// </summary>
    public const int MinorUnitsPerMajor = 100;

    /// <summary>
    /// Trims and uppercases an incoming currency code. Returns the official
    /// currency when the input is null/whitespace.
    /// </summary>
    public static string Normalize(string? currency)
    {
        return string.IsNullOrWhiteSpace(currency)
            ? OfficialCurrency
            : currency.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Returns true when the supplied code is the official currency (case-insensitive).
    /// </summary>
    public static bool IsOfficial(string? currency) =>
        string.Equals(Normalize(currency), OfficialCurrency, StringComparison.Ordinal);

    /// <summary>
    /// Throws <see cref="BusinessRuleException"/> with code <c>UNSUPPORTED_CURRENCY</c>
    /// when the supplied currency is anything other than SAR.
    /// </summary>
    public static void EnsureOfficial(string? currency)
    {
        if (!IsOfficial(currency))
        {
            throw new BusinessRuleException(
                "UNSUPPORTED_CURRENCY",
                $"Unsupported currency: '{currency}'. Only {OfficialCurrency} is allowed.");
        }
    }

    /// <summary>
    /// Converts an SAR major-unit decimal amount to halalas (long), rounding away from zero.
    /// Throws <see cref="BusinessRuleException"/> if currency is not SAR.
    /// </summary>
    public static long ToMinorUnits(decimal amount, string? currency = OfficialCurrency)
    {
        EnsureOfficial(currency);
        var minor = Math.Round(amount * MinorUnitsPerMajor, MidpointRounding.AwayFromZero);
        return checked((long)minor);
    }

    /// <summary>
    /// Converts halalas back to an SAR major-unit decimal rounded to 2 places.
    /// </summary>
    public static decimal FromMinorUnits(long minorAmount, string? currency = OfficialCurrency)
    {
        EnsureOfficial(currency);
        return Math.Round(minorAmount / (decimal)MinorUnitsPerMajor, 2, MidpointRounding.AwayFromZero);
    }
}
