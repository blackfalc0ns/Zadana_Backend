using Microsoft.Extensions.Caching.Distributed;
using System.Globalization;
using System.Text;
using Zadana.Application.Common.Interfaces;

namespace Zadana.Infrastructure.Modules.Identity.Services;

/// <summary>
/// Implementation of <see cref="IJwtRevocationStore"/> backed by the
/// distributed cache (Redis in production, in-memory in dev/testing).
///
/// Two key spaces:
///   - "jwt:revoked:{jti}"          → marker that a single JTI is revoked.
///   - "jwt:user-revoked-before:{userId}" → unix-seconds timestamp; any JWT
///     for that user with iat &lt;= this value is considered revoked.
/// </summary>
public sealed class JwtRevocationStore : IJwtRevocationStore
{
    private const string JtiPrefix = "jwt:revoked:";
    private const string UserPrefix = "jwt:user-revoked-before:";

    private readonly IDistributedCache _cache;

    public JwtRevocationStore(IDistributedCache cache)
    {
        _cache = cache;
    }

    public Task RevokeAsync(string jti, DateTime expiresAtUtc, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jti))
        {
            return Task.CompletedTask;
        }

        var ttl = expiresAtUtc - DateTime.UtcNow;
        if (ttl <= TimeSpan.Zero)
        {
            return Task.CompletedTask;
        }

        return _cache.SetStringAsync(
            JtiPrefix + jti,
            "1",
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl },
            cancellationToken);
    }

    public async Task<bool> IsRevokedAsync(string jti, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jti))
        {
            return false;
        }

        var stored = await _cache.GetStringAsync(JtiPrefix + jti, cancellationToken);
        return !string.IsNullOrEmpty(stored);
    }

    public Task RevokeAllForUserAsync(Guid userId, DateTime revokedBeforeUtc, CancellationToken cancellationToken = default)
    {
        // Keep the marker for at most 90 days — by then every JWT issued
        // before the cutoff has expired naturally (default lifetime is ≤ 1h).
        return _cache.SetStringAsync(
            UserPrefix + userId.ToString("N"),
            new DateTimeOffset(revokedBeforeUtc, TimeSpan.Zero).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(90) },
            cancellationToken);
    }

    public async Task<bool> IsUserRevokedAsync(Guid userId, DateTime issuedAtUtc, CancellationToken cancellationToken = default)
    {
        var stored = await _cache.GetStringAsync(UserPrefix + userId.ToString("N"), cancellationToken);
        if (string.IsNullOrEmpty(stored))
        {
            return false;
        }

        if (!long.TryParse(stored, NumberStyles.Integer, CultureInfo.InvariantCulture, out var revokedBeforeUnix))
        {
            return false;
        }

        var revokedBefore = DateTimeOffset.FromUnixTimeSeconds(revokedBeforeUnix).UtcDateTime;
        return issuedAtUtc <= revokedBefore;
    }
}
