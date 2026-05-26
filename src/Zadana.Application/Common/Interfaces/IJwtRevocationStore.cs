namespace Zadana.Application.Common.Interfaces;

/// <summary>
/// Tracks JWTs that have been explicitly revoked before their natural
/// expiration (e.g., logout, security incident, administrative ban).
///
/// Implementations should use a fast key/value backend such as Redis or
/// memory cache; lookups happen on every authenticated request.
/// </summary>
public interface IJwtRevocationStore
{
    /// <summary>
    /// Marks the given <paramref name="jti"/> as revoked until
    /// <paramref name="expiresAtUtc"/>. After expiry, the JTI is naturally
    /// invalid and can be evicted from the store.
    /// </summary>
    Task RevokeAsync(string jti, DateTime expiresAtUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true when the given <paramref name="jti"/> is in the revoked
    /// list and has not yet expired.
    /// </summary>
    Task<bool> IsRevokedAsync(string jti, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes every JWT issued before the given timestamp for the given
    /// user. Used as a "panic button" by admins or by reuse-detection.
    /// </summary>
    Task RevokeAllForUserAsync(Guid userId, DateTime revokedBeforeUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true when any JWT for the given user issued at or before the
    /// supplied <paramref name="issuedAtUtc"/> has been blanket-revoked.
    /// </summary>
    Task<bool> IsUserRevokedAsync(Guid userId, DateTime issuedAtUtc, CancellationToken cancellationToken = default);
}
