namespace Zadana.Api.Realtime.Contracts;

public sealed record DriverArrivalStateChangedRealtimePayload(
    Guid OrderId,
    string OrderNumber,
    string ArrivalState,
    string DriverName,
    string? ActorRole,
    string TargetUrl,
    DateTime ChangedAtUtc);
