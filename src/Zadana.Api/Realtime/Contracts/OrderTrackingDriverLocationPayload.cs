using System.Text.Json.Serialization;

namespace Zadana.Api.Realtime.Contracts;

/// <summary>
/// Real-time payload broadcast to the order tracking group whenever the assigned driver
/// reports a new GPS location for the order.
/// </summary>
public sealed record OrderTrackingDriverLocationPayload(
    [property: JsonPropertyName("orderId")] Guid OrderId,
    [property: JsonPropertyName("driverId")] Guid DriverId,
    [property: JsonPropertyName("latitude")] decimal Latitude,
    [property: JsonPropertyName("longitude")] decimal Longitude,
    [property: JsonPropertyName("accuracyMeters")] decimal? AccuracyMeters,
    [property: JsonPropertyName("recordedAtUtc")] DateTime RecordedAtUtc);
