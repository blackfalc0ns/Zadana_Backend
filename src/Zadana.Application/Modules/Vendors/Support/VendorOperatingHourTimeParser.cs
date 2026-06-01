using System.Globalization;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Support;

public static class VendorOperatingHourTimeParser
{
    private const string ClockTimeFormat = @"hh\:mm";

    public static bool IsValidClockTime(string? value) =>
        TryParseClockTime(value, out _);

    public static TimeSpan ParseClockTime(string? value)
    {
        if (!TryParseClockTime(value, out var parsed))
        {
            throw new BadRequestException(
                "INVALID_OPERATING_HOUR_TIME",
                "Operating hours must use HH:mm format between 00:00 and 23:59.");
        }

        return parsed;
    }

    private static bool TryParseClockTime(string? value, out TimeSpan parsed)
    {
        parsed = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return TimeSpan.TryParseExact(
            value.Trim(),
            ClockTimeFormat,
            CultureInfo.InvariantCulture,
            out parsed);
    }
}
