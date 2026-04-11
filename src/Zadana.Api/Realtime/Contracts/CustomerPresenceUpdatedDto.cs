namespace Zadana.Api.Realtime.Contracts;

public sealed record CustomerPresenceUpdatedDto(Guid customerId, bool isOnlineNow, DateTime? lastSeenAtUtc);
