using System.Text.Json;
using FluentAssertions;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.SharedKernel.Serialization;

namespace Zadana.UnitTests.Common;

public class SaudiDateTimeJsonConverterTests
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    [Fact]
    public void SerializeUtcDateTime_ShouldWriteSaudiOffset()
    {
        var utc = new DateTime(2026, 6, 21, 15, 30, 0, DateTimeKind.Utc);

        ReadSerializedString(utc).Should().Be("2026-06-21T18:30:00.000+03:00");
    }

    [Fact]
    public void DeserializeSaudiDateTime_ShouldNormalizeToUtc()
    {
        var parsed = JsonSerializer.Deserialize<DateTime>(
            "\"2026-06-21T18:30:00.000+03:00\"",
            Options);

        parsed.Kind.Should().Be(DateTimeKind.Utc);
        parsed.Should().Be(new DateTime(2026, 6, 21, 15, 30, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void SerializeDateTimeOffset_ShouldWriteSaudiOffset()
    {
        var utc = new DateTimeOffset(2026, 6, 21, 15, 30, 0, TimeSpan.Zero);

        ReadSerializedString(utc).Should().Be("2026-06-21T18:30:00.000+03:00");
    }

    [Fact]
    public void DeserializeDateOnly_ShouldPreserveCalendarDate()
    {
        var parsed = JsonSerializer.Deserialize<DateTime>("\"2026-06-21\"", Options);

        parsed.Date.Should().Be(new DateTime(2026, 6, 21));
    }

    [Fact]
    public void DeserializeUnqualifiedTime_ShouldTreatItAsSaudiLocalTime()
    {
        var parsed = JsonSerializer.Deserialize<DateTime>("\"2026-06-21T18:30:00\"", Options);

        parsed.Should().Be(new DateTime(2026, 6, 21, 15, 30, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void SerializeAuditFields_ShouldWriteCreatedAndUpdatedTimesInSaudiTimezone()
    {
        var audit = new
        {
            createdAtUtc = new DateTime(2026, 6, 21, 15, 30, 0, DateTimeKind.Utc),
            updatedAtUtc = new DateTime(2026, 6, 21, 16, 45, 0, DateTimeKind.Utc)
        };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(audit, Options));
        document.RootElement.GetProperty("createdAtUtc").GetString()
            .Should().Be("2026-06-21T18:30:00.000+03:00");
        document.RootElement.GetProperty("updatedAtUtc").GetString()
            .Should().Be("2026-06-21T19:45:00.000+03:00");
    }

    [Fact]
    public void SerializeDriverCompletedOrder_ShouldWriteCompletionTimeInSaudiTimezone()
    {
        var completedOrder = new DriverCompletedOrderListItemDto(
            Guid.NewGuid(),
            "Test merchant",
            null,
            "Test customer",
            new DateTime(2026, 6, 21, 15, 30, 0, DateTimeKind.Utc),
            "delivered",
            100m,
            5m,
            "CashOnDelivery",
            "Riyadh",
            []);

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(completedOrder, Options));

        document.RootElement.GetProperty("completedAtUtc").GetString()
            .Should().Be("2026-06-21T18:30:00.000+03:00");
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        options.Converters.Add(new SaudiDateTimeJsonConverter());
        options.Converters.Add(new SaudiDateTimeOffsetJsonConverter());
        return options;
    }

    private static string? ReadSerializedString<T>(T value)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(value, Options));
        return document.RootElement.GetString();
    }
}
