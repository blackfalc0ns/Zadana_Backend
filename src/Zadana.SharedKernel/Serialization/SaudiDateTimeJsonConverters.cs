using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zadana.SharedKernel.Serialization;

public static class SaudiTime
{
    public static readonly TimeSpan UtcOffset = TimeSpan.FromHours(3);

    public static DateTimeOffset Now => DateTimeOffset.UtcNow.ToOffset(UtcOffset);

    public static DateTime Today => Now.Date;

    public static DateTime StartOfTodayUtc =>
        new DateTimeOffset(Today, UtcOffset).UtcDateTime;

    public static DateTime StartOfTomorrowUtc =>
        new DateTimeOffset(Today.AddDays(1), UtcOffset).UtcDateTime;

    public static DateTimeOffset ToSaudi(DateTime value)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Local => value.ToUniversalTime(),
            DateTimeKind.Utc => value,
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        return new DateTimeOffset(utc).ToOffset(UtcOffset);
    }
}

public sealed class SaudiDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (DateTime.TryParseExact(
                value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var dateOnly))
        {
            return DateTime.SpecifyKind(dateOnly, DateTimeKind.Utc);
        }

        if (!HasExplicitOffset(value) &&
            DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var saudiLocal))
        {
            return new DateTimeOffset(
                    DateTime.SpecifyKind(saudiLocal, DateTimeKind.Unspecified),
                    SaudiTime.UtcOffset)
                .UtcDateTime;
        }

        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
        {
            throw new JsonException("Invalid ISO date/time value.");
        }

        return parsed.UtcDateTime;
    }

    private static bool HasExplicitOffset(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        (value.EndsWith('Z') ||
         (value.Length >= 6 &&
          (value[^6] is '+' or '-') &&
          value[^3] == ':'));

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue(SaudiTime.ToSaudi(value).ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", CultureInfo.InvariantCulture));
}

public sealed class SaudiDateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (DateTime.TryParseExact(
                value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var dateOnly))
        {
            return new DateTimeOffset(dateOnly, SaudiTime.UtcOffset).ToUniversalTime();
        }

        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
        {
            throw new JsonException("Invalid ISO date/time value.");
        }

        return parsed.ToUniversalTime();
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToOffset(SaudiTime.UtcOffset).ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", CultureInfo.InvariantCulture));
}
