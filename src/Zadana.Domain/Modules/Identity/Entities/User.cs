using Microsoft.AspNetCore.Identity;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Identity.Entities;

public class User : IdentityUser<Guid>
{
    public string FullName { get; private set; } = null!;
    public UserRole Role { get; private set; }
    public AccountStatus AccountStatus { get; private set; }
    public bool IsLoginLocked { get; private set; }
    public DateTime? LockedAtUtc { get; private set; }
    public string? LockReason { get; private set; }
    public DateTime? ArchivedAtUtc { get; private set; }
    public string? ArchiveReason { get; private set; }
    
    public string? OtpCode { get; private set; }
    public DateTime? OtpExpiryTime { get; private set; }
    public string? PasswordResetOtp { get; private set; }
    public DateTime? PasswordResetOtpExpiry { get; private set; }
    public DateTime? LastLoginAtUtc { get; private set; }
    public DateTime? LastOtpSentAt { get; private set; }
    public DateTime? LastSeenAtUtc { get; private set; }
    public PresenceState PresenceState { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    // Profile
    public string? ProfilePhotoUrl { get; private set; }
    public string? Address { get; private set; }
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }

    // Navigation
    public ICollection<RefreshToken> RefreshTokens { get; private set; } = [];
    public ICollection<UserPushDevice> PushDevices { get; private set; } = [];

    private User() { }

    public User(
        string fullName,
        string email,
        string phone,
        UserRole role,
        string? profilePhotoUrl = null)
    {
        Id = Guid.NewGuid();
        FullName = fullName.Trim();
        Email = email.ToLowerInvariant().Trim();
        UserName = Email;
        PhoneNumber = phone.Trim();
        Role = role;
        AccountStatus = AccountStatus.Active;
        PresenceState = PresenceState.Offline;
        IsLoginLocked = false;
        EmailConfirmed = false;
        PhoneNumberConfirmed = false;
        ProfilePhotoUrl = profilePhotoUrl;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateProfile(string fullName, string email, string phone)
    {
        FullName = fullName.Trim();
        Email = email.ToLowerInvariant().Trim();
        UserName = Email;
        PhoneNumber = phone.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void VerifyEmail() => EmailConfirmed = true;
    public void VerifyPhone() => PhoneNumberConfirmed = true;

    public void RecordLogin() => LastLoginAtUtc = DateTime.UtcNow;
    public void RecordActivity() => LastLoginAtUtc = DateTime.UtcNow;
    public void MarkPresenceOnline(DateTime timestampUtc)
    {
        PresenceState = PresenceState.Online;
        LastSeenAtUtc = timestampUtc;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkPresenceOffline(DateTime timestampUtc)
    {
        PresenceState = PresenceState.Offline;
        LastSeenAtUtc = timestampUtc;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Suspend()
    {
        AccountStatus = AccountStatus.Suspended;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Activate()
    {
        AccountStatus = AccountStatus.Active;
        if (!IsArchived())
        {
            IsLoginLocked = false;
            LockedAtUtc = null;
            LockReason = null;
        }

        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Ban()
    {
        AccountStatus = AccountStatus.Banned;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        AccountStatus = AccountStatus.Inactive;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void LockLogin(string reason)
    {
        IsLoginLocked = true;
        LockedAtUtc = DateTime.UtcNow;
        LockReason = reason.Trim();
        AccountStatus = AccountStatus.Suspended;
        LockoutEnabled = true;
        LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UnlockLogin()
    {
        IsLoginLocked = false;
        LockedAtUtc = null;
        LockReason = null;
        LockoutEnd = null;

        if (!IsArchived() && AccountStatus == AccountStatus.Suspended)
        {
            AccountStatus = AccountStatus.Active;
        }

        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Archive(string reason)
    {
        ArchivedAtUtc = DateTime.UtcNow;
        ArchiveReason = reason.Trim();
        IsLoginLocked = true;
        LockedAtUtc ??= DateTime.UtcNow;
        LockReason ??= reason.Trim();
        AccountStatus = AccountStatus.Inactive;
        LockoutEnabled = true;
        LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public bool IsArchived() => ArchivedAtUtc.HasValue;

    // --- OTP Domain Behavior ---
    public string GenerateOtp()
    {
        // Generate a 4-digit code (simple string randomizer)
        var random = new Random();
        OtpCode = random.Next(1000, 9999).ToString();
        OtpExpiryTime = DateTime.UtcNow.AddMinutes(5); // Valid for 5 minutes
        LastOtpSentAt = DateTime.UtcNow;
        return OtpCode;
    }

    public bool CanResendOtp()
    {
        if (LastOtpSentAt == null) return true;
        return DateTime.UtcNow >= LastOtpSentAt.Value.AddMinutes(1);
    }

    public bool VerifyOtp(string code)
    {
        if (string.IsNullOrWhiteSpace(OtpCode) || OtpExpiryTime == null)
            return false;

        if (DateTime.UtcNow > OtpExpiryTime.Value)
            return false; // Expired

        if (OtpCode != code.Trim())
            return false; // Incorrect

        // Success: Clear the OTP and mark as verified
        OtpCode = null;
        OtpExpiryTime = null;
        PhoneNumberConfirmed = true; // Assuming OTP is primarily for Phone in this scenario
        UpdatedAtUtc = DateTime.UtcNow;
        
        return true;
    }

    // --- Password Reset Domain Behavior ---
    public string GeneratePasswordResetOtp()
    {
        var random = new Random();
        PasswordResetOtp = random.Next(1000, 9999).ToString();
        PasswordResetOtpExpiry = DateTime.UtcNow.AddMinutes(15); // Valid for 15 minutes
        LastOtpSentAt = DateTime.UtcNow;
        return PasswordResetOtp;
    }

    public bool VerifyPasswordResetOtp(string code)
    {
        if (string.IsNullOrWhiteSpace(PasswordResetOtp) || PasswordResetOtpExpiry == null)
            return false;

        if (DateTime.UtcNow > PasswordResetOtpExpiry.Value)
            return false; // Expired

        if (PasswordResetOtp != code.Trim())
            return false; // Incorrect

        // Success: Clear the Reset OTP
        PasswordResetOtp = null;
        PasswordResetOtpExpiry = null;
        UpdatedAtUtc = DateTime.UtcNow;
        
        return true;
    }
}
