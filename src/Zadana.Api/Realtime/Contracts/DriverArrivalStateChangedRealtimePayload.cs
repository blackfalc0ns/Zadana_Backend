using System.Text.Json.Serialization;

namespace Zadana.Api.Realtime.Contracts;

public sealed record DriverArrivalStateChangedRealtimePayload(
    [property: JsonPropertyName("orderId")] Guid OrderId,
    [property: JsonPropertyName("orderNumber")] string OrderNumber,
    [property: JsonPropertyName("arrivalState")] string ArrivalState,
    [property: JsonPropertyName("driverName")] string DriverName,
    [property: JsonPropertyName("actorRole")] string? ActorRole,
    [property: JsonPropertyName("targetUrl")] string TargetUrl,
    [property: JsonPropertyName("changedAtUtc")] DateTime ChangedAtUtc);
