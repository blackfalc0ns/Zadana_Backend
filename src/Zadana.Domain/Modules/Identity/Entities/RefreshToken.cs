using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Identity.Entities;

public class RefreshToken : BaseEntity
{
    public Guid UserId { get; private set; }
    public string Token { get; private set; } = null!;
    public DateTime ExpiresAtUtc { get; private set; }
    public bool IsRevoked { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }

    // Navigation
    public User User { get; private set; } = null!;

    private RefreshToken() { }

    public RefreshToken(Guid userId, string token, DateTime expiresAtUtc)
    {
        UserId = userId;
        Token = token;
        ExpiresAtUtc = expiresAtUtc;
        IsRevoked = false;
    }

    public void Revoke()
    {
        IsRevoked = true;
        RevokedAtUtc = DateTime.UtcNow;
    }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAtUtc;
    public bool IsActive => !IsRevoked && !IsExpired;
}
