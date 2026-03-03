using Zadana.Domain.Modules.Identity.Enums;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Identity.Entities;

public class User : BaseEntity
{
    public string FullName { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public string Phone { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public UserRole Role { get; private set; }
    public AccountStatus AccountStatus { get; private set; }
    public bool IsEmailVerified { get; private set; }
    public bool IsPhoneVerified { get; private set; }
    public DateTime? LastLoginAtUtc { get; private set; }

    // Profile
    public string? ProfilePhotoUrl { get; private set; }
    public string? Address { get; private set; }
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }

    // Navigation
    public ICollection<RefreshToken> RefreshTokens { get; private set; } = [];

    private User() { }

    public User(
        string fullName,
        string email,
        string phone,
        string passwordHash,
        UserRole role,
        string? profilePhotoUrl = null,
        string? address = null,
        decimal? latitude = null,
        decimal? longitude = null)
    {
        FullName = fullName.Trim();
        Email = email.ToLowerInvariant().Trim();
        Phone = phone.Trim();
        PasswordHash = passwordHash;
        Role = role;
        AccountStatus = AccountStatus.Active;
        IsEmailVerified = false;
        IsPhoneVerified = false;
        ProfilePhotoUrl = profilePhotoUrl;
        Address = address?.Trim();
        Latitude = latitude;
        Longitude = longitude;
    }

    public void UpdateProfile(string fullName, string phone)
    {
        FullName = fullName.Trim();
        Phone = phone.Trim();
    }

    public void ChangePassword(string newPasswordHash)
    {
        PasswordHash = newPasswordHash;
    }

    public void VerifyEmail() => IsEmailVerified = true;
    public void VerifyPhone() => IsPhoneVerified = true;

    public void RecordLogin() => LastLoginAtUtc = DateTime.UtcNow;

    public void Suspend() => AccountStatus = AccountStatus.Suspended;
    public void Activate() => AccountStatus = AccountStatus.Active;
    public void Ban() => AccountStatus = AccountStatus.Banned;
    public void Deactivate() => AccountStatus = AccountStatus.Inactive;
}
