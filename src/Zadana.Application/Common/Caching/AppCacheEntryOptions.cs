namespace Zadana.Application.Common.Caching;

public sealed record AppCacheEntryOptions(
    TimeSpan Expiration,
    TimeSpan? LocalExpiration = null);
