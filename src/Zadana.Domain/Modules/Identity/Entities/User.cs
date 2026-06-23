using Microsoft.AspNetCore.Identity;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Identity.Entities;

public class User : IdentityUser<Guid>
{
    public string FullName { get; private set; } = null!;
    public UserRole Role { get; private set; }
    public int PermissionVersion { get; private set; }
    public AccountStatus AccountStatus { get; private set; }
    public bool IsLoginLocked { get; private set; }
    public DateTime? LockedAtUtc { get; private set; }
    public string? LockReason { get; private set; }
    public DateTime? ArchivedAtUtc { get; private set; }
    public string? ArchiveReason { get; private set; }
    public string? Department { get; private set; }
    public string? Team { get; private set; }
    public bool MustChangePassword { get; private set; }
    public DateTime? TemporaryPasswordIssuedAtUtc { get; private set; }
    public DateTime? LastPasswordChangedAtUtc { get; private set; }
    
    public string? OtpCode { get; private set; }
    public DateTime? OtpExpiryTime { get; private set; }
    public int OtpAttempts { get; private set; }
    public int OtpLockoutCount { get; private set; }
    public DateTime? OtpLockedUntilUtc { get; private set; }
    public string? PasswordResetOtp { get; private set; }
    public DateTime? PasswordResetOtpExpiry { get; private set; }
    public int PasswordResetOtpAttempts { get; private set; }
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

    // Communication Profile
    public string PreferredLocale { get; private set; } = "ar";
    public string? ReplyTo { get; private set; }
    public string? NotificationEmailsJson { get; private set; }
    public string? EscalationEmailsJson { get; private set; }
    public string? EmailOptInJson { get; private set; }

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
        PermissionVersion = 1;
        AccountStatus = AccountStatus.Active;
        PresenceState = PresenceState.Offline;
        IsLoginLocked = false;
        MustChangePassword = false;
        LockoutEnabled = true;
        EmailConfirmed = false;
        PhoneNumberConfirmed = false;
        ProfilePhotoUrl = profilePhotoUrl;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateProfile(string fullName, string email, string phone)
    {
        FullName = fullName.Trim();
        var normalizedEmail = email.ToLowerInvariant().Trim();
        var emailChanged = !string.Equals(Email, normalizedEmail, StringComparison.OrdinalIgnoreCase);
        Email = normalizedEmail;
        UserName = Email;
        NormalizedEmail = normalizedEmail.ToUpperInvariant();
        PhoneNumber = phone.Trim();
        UpdatedAtUtc = DateTime.UtcNow;

        if (emailChanged)
        {
            EmailConfirmed = false;
        }
    }

    public void UpdateProfilePhoto(string? profilePhotoUrl)
    {
        ProfilePhotoUrl = string.IsNullOrWhiteSpace(profilePhotoUrl)
            ? null
            : profilePhotoUrl.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void VerifyEmail() => EmailConfirmed = true;
    public void VerifyPhone() => PhoneNumberConfirmed = true;

    public void UpdateDirectoryProfile(string? department, string? team)
    {
        Department = string.IsNullOrWhiteSpace(department) ? null : department.Trim();
        Team = string.IsNullOrWhiteSpace(team) ? null : team.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateCommunicationProfile(
        string preferredLocale,
        string? replyTo,
        List<string>? notificationEmails,
        List<string>? escalationEmails,
        object? emailOptIn)
    {
        PreferredLocale = string.IsNullOrWhiteSpace(preferredLocale) ? "ar" : preferredLocale.Trim();
        ReplyTo = string.IsNullOrWhiteSpace(replyTo) ? null : replyTo.Trim();
        NotificationEmailsJson = notificationEmails != null ? System.Text.Json.JsonSerializer.Serialize(notificationEmails) : null;
        EscalationEmailsJson = escalationEmails != null ? System.Text.Json.JsonSerializer.Serialize(escalationEmails) : null;
        EmailOptInJson = emailOptIn != null ? System.Text.Json.JsonSerializer.Serialize(emailOptIn) : null;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateRole(UserRole role)
    {
        if (Role == role)
        {
            return;
        }

        Role = role;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void RequirePasswordChange()
    {
        MustChangePassword = true;
        TemporaryPasswordIssuedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void CompletePasswordChange()
    {
        MustChangePassword = false;
        TemporaryPasswordIssuedAtUtc = null;
        LastPasswordChangedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void IncrementPermissionVersion()
    {
        PermissionVersion++;
        UpdatedAtUtc = DateTime.UtcNow;
    }

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
    // OTP code is now stored as a SHA-256 hash; the plaintext is generated
    // here, returned to the caller (so it can be sent via SMS/email) and
    // immediately discarded. Random.Shared internally uses a thread-safe,
    // cryptographically-strong source on .NET 9.
    private const int MaxOtpAttempts = 5;
    private const int OtpExhaustionLockoutThreshold = 3;
    private static readonly TimeSpan OtpAccountLockoutDuration = TimeSpan.FromMinutes(60);

    public bool IsOtpAccountLocked()
    {
        return OtpLockedUntilUtc.HasValue && OtpLockedUntilUtc.Value > DateTime.UtcNow;
    }

    public string GenerateOtp()
    {
        if (IsOtpAccountLocked())
        {
            throw new InvalidOperationException("OTP_ACCOUNT_LOCKED");
        }

        ClearPasswordResetSession();

        var code = GenerateNumericCode(4);
        OtpCode = HashOtp(code);
        OtpExpiryTime = DateTime.UtcNow.AddMinutes(5);
        OtpAttempts = 0;
        LastOtpSentAt = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
        return code;
    }

    public bool CanResendOtp()
    {
        if (LastOtpSentAt == null) return true;
        return DateTime.UtcNow >= LastOtpSentAt.Value.AddMinutes(1);
    }

    public bool VerifyOtp(string code)
    {
        if (IsOtpAccountLocked())
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(OtpCode) || OtpExpiryTime == null)
            return false;

        if (DateTime.UtcNow > OtpExpiryTime.Value)
        {
            // Expired — clear so a new OTP must be requested.
            ExhaustOtp();
            return false;
        }

        if (OtpAttempts >= MaxOtpAttempts)
        {
            // Too many failed attempts — invalidate the OTP entirely.
            ExhaustOtp();
            return false;
        }

        var providedHash = HashOtp(code?.Trim() ?? string.Empty);
        if (!FixedTimeEquals(OtpCode, providedHash))
        {
            OtpAttempts++;
            if (OtpAttempts >= MaxOtpAttempts)
            {
                ExhaustOtp();
            }
            UpdatedAtUtc = DateTime.UtcNow;
            return false;
        }

        // Success: clear OTP state and mark email confirmed.
        OtpCode = null;
        OtpExpiryTime = null;
        OtpAttempts = 0;
        OtpLockoutCount = 0;
        OtpLockedUntilUtc = null;
        EmailConfirmed = true;
        UpdatedAtUtc = DateTime.UtcNow;

        return true;
    }

    private void ExhaustOtp()
    {
        OtpCode = null;
        OtpExpiryTime = null;
        OtpAttempts = 0;
        OtpLockoutCount++;
        if (OtpLockoutCount >= OtpExhaustionLockoutThreshold)
        {
            OtpLockedUntilUtc = DateTime.UtcNow.Add(OtpAccountLockoutDuration);
            OtpLockoutCount = 0;
        }
        UpdatedAtUtc = DateTime.UtcNow;
    }

    // --- Password Reset Domain Behavior ---
    public const int PasswordResetProofLifetimeMinutes = 10;

    public string GeneratePasswordResetOtp()
    {
        ClearRegistrationOtp();

        var code = GenerateNumericCode(4);
        PasswordResetOtp = HashOtp(code);
        PasswordResetOtpExpiry = DateTime.UtcNow.AddMinutes(15);
        PasswordResetOtpAttempts = 0;
        LastOtpSentAt = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
        return code;
    }

    public bool HasActivePasswordResetOtp() =>
        !string.IsNullOrWhiteSpace(PasswordResetOtp)
        && PasswordResetOtpExpiry.HasValue
        && DateTime.UtcNow <= PasswordResetOtpExpiry.Value;

    public bool HasPendingRegistrationVerification() => !EmailConfirmed;

    public void ClearRegistrationOtp()
    {
        OtpCode = null;
        OtpExpiryTime = null;
        OtpAttempts = 0;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public bool VerifyPasswordResetOtp(string code)
    {
        if (!TryValidatePasswordResetOtpCode(code))
        {
            return false;
        }

        ClearPasswordResetSession();
        return true;
    }

    public string? ConfirmPasswordResetOtp(string code)
    {
        if (!TryValidatePasswordResetOtpCode(code))
        {
            return null;
        }

        var proofToken = GenerateProofToken();
        PasswordResetOtp = HashOtp(proofToken);
        PasswordResetOtpExpiry = DateTime.UtcNow.AddMinutes(PasswordResetProofLifetimeMinutes);
        PasswordResetOtpAttempts = 0;
        UpdatedAtUtc = DateTime.UtcNow;
        return proofToken;
    }

    public bool ValidatePasswordResetProof(string proofToken)
    {
        if (string.IsNullOrWhiteSpace(PasswordResetOtp) || PasswordResetOtpExpiry == null)
        {
            return false;
        }

        if (DateTime.UtcNow > PasswordResetOtpExpiry.Value)
        {
            ClearPasswordResetSession();
            return false;
        }

        var providedHash = HashOtp(proofToken?.Trim() ?? string.Empty);
        return FixedTimeEquals(PasswordResetOtp, providedHash);
    }

    public void ClearPasswordResetSession()
    {
        PasswordResetOtp = null;
        PasswordResetOtpExpiry = null;
        PasswordResetOtpAttempts = 0;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private bool TryValidatePasswordResetOtpCode(string code)
    {
        if (string.IsNullOrWhiteSpace(PasswordResetOtp) || PasswordResetOtpExpiry == null)
        {
            return false;
        }

        if (DateTime.UtcNow > PasswordResetOtpExpiry.Value)
        {
            ClearPasswordResetSession();
            return false;
        }

        if (PasswordResetOtpAttempts >= MaxOtpAttempts)
        {
            ClearPasswordResetSession();
            return false;
        }

        var providedHash = HashOtp(code?.Trim() ?? string.Empty);
        if (!FixedTimeEquals(PasswordResetOtp, providedHash))
        {
            PasswordResetOtpAttempts++;
            UpdatedAtUtc = DateTime.UtcNow;
            if (PasswordResetOtpAttempts >= MaxOtpAttempts)
            {
                ClearPasswordResetSession();
            }

            return false;
        }

        return true;
    }

    private static string GenerateProofToken()
    {
        var bytes = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes);
    }

    private static string GenerateNumericCode(int length)
    {
        // RandomNumberGenerator.GetInt32 is uniform and CSPRNG-backed.
        var min = (int)Math.Pow(10, length - 1);
        var max = (int)Math.Pow(10, length);
        var value = System.Security.Cryptography.RandomNumberGenerator.GetInt32(min, max);
        return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string HashOtp(string code)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(code);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static bool FixedTimeEquals(string? a, string? b)
    {
        if (a is null || b is null) return false;
        var ba = System.Text.Encoding.ASCII.GetBytes(a);
        var bb = System.Text.Encoding.ASCII.GetBytes(b);
        return ba.Length == bb.Length &&
               System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(ba, bb);
    }
}
