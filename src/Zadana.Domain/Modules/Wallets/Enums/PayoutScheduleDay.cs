using Zadana.SharedKernel.Exceptions;

namespace Zadana.Domain.Modules.Wallets.Enums;

/// <summary>
/// A beneficiary's preferred weekday for settlements. The platform-wide list
/// of enabled days is configured separately in <c>SettlementProcessingSettings</c>.
/// The persisted numeric values intentionally match <see cref="DayOfWeek"/>.
/// </summary>
public enum PayoutScheduleDay
{
    Sunday = (int)DayOfWeek.Sunday,
    Monday = (int)DayOfWeek.Monday,
    Tuesday = (int)DayOfWeek.Tuesday,
    Wednesday = (int)DayOfWeek.Wednesday,
    Thursday = (int)DayOfWeek.Thursday,
    Friday = (int)DayOfWeek.Friday,
    Saturday = (int)DayOfWeek.Saturday
}

public static class PayoutScheduleDayPolicy
{
    /// <summary>
    /// Backwards-compatible platform defaults for deployments that have not
    /// yet persisted the singleton processing settings row.
    /// </summary>
    public static readonly IReadOnlyList<PayoutScheduleDay> DefaultPayoutDays =
        [PayoutScheduleDay.Monday, PayoutScheduleDay.Thursday];

    public static bool IsAllowed(PayoutScheduleDay payoutDay) =>
        Enum.IsDefined(payoutDay);

    public static bool TryParse(string? value, out PayoutScheduleDay payoutDay)
    {
        payoutDay = default;
        return !string.IsNullOrWhiteSpace(value) &&
               Enum.TryParse(value.Trim(), ignoreCase: true, out payoutDay) &&
               IsAllowed(payoutDay);
    }

    public static PayoutScheduleDay ParseOrDefault(
        string? value,
        PayoutScheduleDay fallback = PayoutScheduleDay.Monday)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        if (TryParse(value, out var payoutDay))
        {
            return payoutDay;
        }

        throw new BusinessRuleException(
            "INVALID_PAYOUT_DAY",
            "Payout day must be a valid day of the week.");
    }

    public static PayoutScheduleDay EnsureAllowed(PayoutScheduleDay payoutDay)
    {
        if (!IsAllowed(payoutDay))
        {
            throw new BusinessRuleException(
                "INVALID_PAYOUT_DAY",
                "Payout day must be a valid day of the week.");
        }

        return payoutDay;
    }

    public static bool IsPayoutDay(DateTime date, PayoutScheduleDay payoutDay) =>
        date.DayOfWeek == (DayOfWeek)payoutDay;

    public static DateTime NextOnOrAfter(DateTime date, PayoutScheduleDay payoutDay)
    {
        EnsureAllowed(payoutDay);
        var offset = ((int)payoutDay - (int)date.DayOfWeek + 7) % 7;
        return date.Date.AddDays(offset);
    }

    public static IReadOnlyList<PayoutScheduleDay> NormalizeDays(
        IEnumerable<PayoutScheduleDay>? payoutDays,
        bool useDefaultWhenNull = false)
    {
        if (payoutDays is null)
        {
            return useDefaultWhenNull
                ? DefaultPayoutDays.ToArray()
                : [];
        }

        var normalized = payoutDays
            .Distinct()
            .OrderBy(day => (int)day)
            .ToArray();

        if (normalized.Any(day => !IsAllowed(day)))
        {
            throw new BusinessRuleException(
                "INVALID_PAYOUT_DAY",
                "Payout days must contain valid days of the week only.");
        }

        return normalized;
    }

    /// <summary>
    /// Resolves a stable fallback when an administrator disables a day.
    /// Monday remains the preferred fallback when enabled to preserve the
    /// existing platform behaviour; otherwise the earliest enabled weekday is
    /// selected by its <see cref="DayOfWeek"/> value.
    /// </summary>
    public static PayoutScheduleDay ResolveFallback(IEnumerable<PayoutScheduleDay> payoutDays)
    {
        var normalized = NormalizeDays(payoutDays);
        if (normalized.Count == 0)
        {
            throw new BusinessRuleException(
                "PAYOUT_DAYS_REQUIRED",
                "At least one payout day must be enabled.");
        }

        return normalized.Contains(PayoutScheduleDay.Monday)
            ? PayoutScheduleDay.Monday
            : normalized[0];
    }
}
