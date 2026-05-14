namespace Zadana.Api.Realtime.Contracts;

public sealed record DriverLocationUpdatedRealtimePayload(
    Guid OrderId,
    string OrderNumber,
    string TrackingStatus,
    string DriverProgressStatus,
    int? EtaMinutes,
    DateTime? LastLocationUpdateUtc,
    bool IsDriverLocationStale,
    decimal? DistanceToCustomerKm,
    string? DriverProximityLabel,
    string DriverArrivalState,
    string TargetUrl,
    DateTime ChangedAtUtc);
