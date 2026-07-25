using Zadana.Domain.Modules.Vendors.Entities;

namespace Zadana.Application.Modules.Vendors.Support;

public static class BranchOperatingHoursSupport
{
    private static readonly TimeSpan DeadlineGraceAfterOpen = TimeSpan.FromHours(2);

    public static bool IsBranchOpenAt(IReadOnlyList<BranchOperatingHour> hours, DateTime utcNow)
    {
        if (hours.Count == 0)
        {
            return true;
        }

        var localNow = ToLocal(utcNow);
        return IsWithinWorkingHours(hours, localNow);
    }

    public static DateTime ResolveExtendedDeadlineUtc(IReadOnlyList<BranchOperatingHour> hours, DateTime utcNow)
    {
        var nextOpenLocal = ResolveNextOpenLocal(hours, ToLocal(utcNow));
        var nextOpenUtc = ToUtc(nextOpenLocal);
        return nextOpenUtc.Add(DeadlineGraceAfterOpen);
    }

    public static string? BuildHoursTodayLabel(IReadOnlyList<BranchOperatingHour> hours, DateTime utcNow)
    {
        if (hours.Count == 0)
        {
            return null;
        }

        var localNow = ToLocal(utcNow);
        var today = (int)localNow.DayOfWeek;
        var todayHour = hours.FirstOrDefault(hour => hour.DayOfWeek == today);

        if (todayHour is null || todayHour.IsClosed)
        {
            return "Closed today";
        }

        return $"{FormatTime(todayHour.OpenTime)} - {FormatTime(todayHour.CloseTime)}";
    }

    private static bool IsWithinWorkingHours(IReadOnlyList<BranchOperatingHour> hours, DateTime localNow)
    {
        var currentDay = (int)localNow.DayOfWeek;
        var currentTime = localNow.TimeOfDay;

        foreach (var hour in hours)
        {
            if (hour.IsClosed)
            {
                continue;
            }

            if (hour.CloseTime >= hour.OpenTime)
            {
                if (hour.DayOfWeek == currentDay &&
                    currentTime >= hour.OpenTime &&
                    currentTime <= hour.CloseTime)
                {
                    return true;
                }

                continue;
            }

            if (hour.DayOfWeek == currentDay && currentTime >= hour.OpenTime)
            {
                return true;
            }

            var nextDay = (hour.DayOfWeek + 1) % 7;
            if (nextDay == currentDay && currentTime <= hour.CloseTime)
            {
                return true;
            }
        }

        return false;
    }

    private static DateTime ResolveNextOpenLocal(IReadOnlyList<BranchOperatingHour> hours, DateTime localNow)
    {
        if (hours.Count == 0)
        {
            return localNow.Add(DeadlineGraceAfterOpen);
        }

        for (var dayOffset = 0; dayOffset <= 7; dayOffset++)
        {
            var candidateDate = localNow.Date.AddDays(dayOffset);
            var dayOfWeek = (int)candidateDate.DayOfWeek;
            var dayHours = hours
                .Where(hour => hour.DayOfWeek == dayOfWeek && !hour.IsClosed)
                .OrderBy(hour => hour.OpenTime)
                .ToList();

            foreach (var hour in dayHours)
            {
                var openAt = candidateDate.Add(hour.OpenTime);
                if (openAt > localNow)
                {
                    return openAt;
                }
            }
        }

        return localNow.AddDays(1);
    }

    private static DateTime ToLocal(DateTime utcNow) =>
        TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utcNow, DateTimeKind.Utc),
            ResolveSaudiTimeZone());

    private static DateTime ToUtc(DateTime localTime) =>
        TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified),
            ResolveSaudiTimeZone());

    private static TimeZoneInfo ResolveSaudiTimeZone()
    {
        foreach (var id in new[] { "Asia/Riyadh", "Arab Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone(
            "UTC+3",
            TimeSpan.FromHours(3),
            "UTC+3",
            "UTC+3");
    }

    private static string FormatTime(TimeSpan time) =>
        time.ToString(@"hh\:mm");
}
