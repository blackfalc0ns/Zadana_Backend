namespace Zadana.Api.Realtime.Contracts;

public sealed record OrderStatusChangedRealtimePayload(
    Guid OrderId,
    string OrderNumber,
    Guid VendorId,
    string OldStatus,
    string NewStatus,
    string? ActorRole,
    string Action,
    string TargetUrl,
    DateTime ChangedAtUtc);
