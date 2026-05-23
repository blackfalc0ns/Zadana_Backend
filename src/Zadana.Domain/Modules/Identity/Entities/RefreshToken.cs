using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Identity.Entities;

public class RefreshToken : BaseEntity
{
    public Guid UserId { get; private set; }

    /// <summary>
    /// Legacy column. New tokens are stored in <see cref="TokenHash"/>; this
    /// remains nullable to preserve backward compatibility with rows created
    /// before hashing was introduced.
    /// </summary>
    public string? Token { get; private set; }

    /// <summary>
    /// SHA-256 hex digest of the issued refresh token. The plaintext is never
    /// persisted for tokens written under the new path.
    /// </summary>
    public string? TokenHash { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }
    public bool IsRevoked { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }

    /// <summary>
    /// Marks rows that have already been rotated and replayed. Used to detect
    /// reuse of an old refresh token after it has been swapped for a new one.
    /// </summary>
    public bool WasReused { get; private set; }

    // Navigation
    public User User { get; private set; } = null!;

    private RefreshToken() { }

    /// <summary>
    /// Legacy constructor: token is stored as plaintext in <see cref="Token"/>.
    /// Kept only for migrations / tests; production code paths now use
    /// <see cref="CreateHashed"/>.
    /// </summary>
    public RefreshToken(Guid userId, string token, DateTime expiresAtUtc)
    {
        UserId = userId;
        Token = token;
        ExpiresAtUtc = expiresAtUtc;
        IsRevoked = false;
    }

    public static RefreshToken CreateHashed(Guid userId, string tokenHash, DateTime expiresAtUtc)
    {
        return new RefreshToken
        {
            UserId = userId,
            Token = null,
            TokenHash = tokenHash,
            ExpiresAtUtc = expiresAtUtc,
            IsRevoked = false
        };
    }

    public void Revoke()
    {
        IsRevoked = true;
        RevokedAtUtc = DateTime.UtcNow;
    }

    public void MarkReused()
    {
        WasReused = true;
        IsRevoked = true;
        RevokedAtUtc = DateTime.UtcNow;
    }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAtUtc;
    public bool IsActive => !IsRevoked && !IsExpired;
}
