using Zadana.SharedKernel.Exceptions;

namespace Zadana.Domain.Modules.Wallets.Enums;

/// <summary>
/// The only weekdays on which the platform processes beneficiary payouts.
/// </summary>
public enum PayoutScheduleDay
{
    Monday = (int)DayOfWeek.Monday,
    Thursday = (int)DayOfWeek.Thursday
}

public static class PayoutScheduleDayPolicy
{
    public static bool IsAllowed(PayoutScheduleDay payoutDay) =>
        payoutDay is PayoutScheduleDay.Monday or PayoutScheduleDay.Thursday;

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
            "Payout day must be either Monday or Thursday.");
    }

    public static PayoutScheduleDay EnsureAllowed(PayoutScheduleDay payoutDay)
    {
        if (!IsAllowed(payoutDay))
        {
            throw new BusinessRuleException(
                "INVALID_PAYOUT_DAY",
                "Payout day must be either Monday or Thursday.");
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
}
