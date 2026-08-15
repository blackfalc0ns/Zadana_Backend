using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Domain.Modules.Identity.Entities;

/// <summary>
/// In-memory signup session used while OTP is outstanding.
/// Serialized into a signed registration token — never persisted to the database.
/// </summary>
public class PendingRegistration
{
    public const int MaxOtpAttempts = 5;
    public static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(24);
    public static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan ResendCooldown = TimeSpan.FromMinutes(1);

    public Guid Id { get; private set; }
    public string Email { get; private set; } = null!;
    public string? PhoneNumber { get; private set; }
    public string PasswordHash { get; private set; } = null!;
    public string FullName { get; private set; } = null!;
    public UserRole Role { get; private set; }
    public Guid? ExistingUserId { get; private set; }
    public string? LinkedOtpEmail { get; private set; }
    public string PayloadJson { get; private set; } = null!;
    public string? ProfilePhotoUrl { get; private set; }
    public string? OtpCodeHash { get; private set; }
    public DateTime? OtpExpiryUtc { get; private set; }
    public int OtpAttempts { get; private set; }
    public DateTime? LastOtpSentAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }

    private PendingRegistration()
    {
    }

    public PendingRegistration(
        string email,
        string? phoneNumber,
        string passwordHash,
        string fullName,
        UserRole role,
        string payloadJson,
        string? profilePhotoUrl = null,
        Guid? existingUserId = null,
        string? linkedOtpEmail = null)
    {
        Id = Guid.NewGuid();
        Email = email.Trim().ToLowerInvariant();
        PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber?.Trim();
        PasswordHash = passwordHash;
        FullName = fullName.Trim();
        Role = role;
        ExistingUserId = existingUserId;
        LinkedOtpEmail = string.IsNullOrWhiteSpace(linkedOtpEmail) ? null : linkedOtpEmail.Trim().ToLowerInvariant();
        PayloadJson = payloadJson;
        ProfilePhotoUrl = string.IsNullOrWhiteSpace(profilePhotoUrl) ? null : profilePhotoUrl.Trim();
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
        ExpiresAtUtc = CreatedAtUtc.Add(DefaultTtl);
    }

    public static PendingRegistration Rehydrate(
        Guid id,
        string email,
        string? phoneNumber,
        string passwordHash,
        string fullName,
        UserRole role,
        string payloadJson,
        string? profilePhotoUrl,
        string? otpCodeHash,
        DateTime? otpExpiryUtc,
        int otpAttempts,
        DateTime? lastOtpSentAtUtc,
        DateTime createdAtUtc,
        DateTime updatedAtUtc,
        DateTime expiresAtUtc,
        Guid? existingUserId = null,
        string? linkedOtpEmail = null) =>
        new()
        {
            Id = id,
            Email = email,
            PhoneNumber = phoneNumber,
            PasswordHash = passwordHash,
            FullName = fullName,
            Role = role,
            ExistingUserId = existingUserId,
            LinkedOtpEmail = linkedOtpEmail,
            PayloadJson = payloadJson,
            ProfilePhotoUrl = profilePhotoUrl,
            OtpCodeHash = otpCodeHash,
            OtpExpiryUtc = otpExpiryUtc,
            OtpAttempts = otpAttempts,
            LastOtpSentAtUtc = lastOtpSentAtUtc,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = updatedAtUtc,
            ExpiresAtUtc = expiresAtUtc
        };

    public bool IsExpired() => DateTime.UtcNow > ExpiresAtUtc;

    public bool CanResendOtp()
    {
        if (LastOtpSentAtUtc is null)
        {
            return true;
        }

        return DateTime.UtcNow >= LastOtpSentAtUtc.Value.Add(ResendCooldown);
    }

    public int? ResendCooldownSecondsRemaining()
    {
        if (LastOtpSentAtUtc is null)
        {
            return null;
        }

        var readyAt = LastOtpSentAtUtc.Value.Add(ResendCooldown);
        if (DateTime.UtcNow >= readyAt)
        {
            return null;
        }

        return (int)Math.Ceiling((readyAt - DateTime.UtcNow).TotalSeconds);
    }

    public string GenerateOtp()
    {
        var code = GenerateNumericCode(4);
        OtpCodeHash = HashOtp(code);
        OtpExpiryUtc = DateTime.UtcNow.Add(OtpLifetime);
        OtpAttempts = 0;
        LastOtpSentAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
        return code;
    }

    public bool VerifyOtp(string code)
    {
        if (IsExpired() || string.IsNullOrWhiteSpace(OtpCodeHash) || OtpExpiryUtc is null)
        {
            return false;
        }

        if (DateTime.UtcNow > OtpExpiryUtc.Value)
        {
            ClearOtp();
            return false;
        }

        if (OtpAttempts >= MaxOtpAttempts)
        {
            ClearOtp();
            return false;
        }

        var providedHash = HashOtp(code?.Trim() ?? string.Empty);
        if (!FixedTimeEquals(OtpCodeHash, providedHash))
        {
            OtpAttempts++;
            if (OtpAttempts >= MaxOtpAttempts)
            {
                ClearOtp();
            }

            UpdatedAtUtc = DateTime.UtcNow;
            return false;
        }

        ClearOtp();
        return true;
    }

    private void ClearOtp()
    {
        OtpCodeHash = null;
        OtpExpiryUtc = null;
        OtpAttempts = 0;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private static string GenerateNumericCode(int length)
    {
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
        if (a is null || b is null)
        {
            return false;
        }

        var ba = System.Text.Encoding.ASCII.GetBytes(a);
        var bb = System.Text.Encoding.ASCII.GetBytes(b);
        return ba.Length == bb.Length &&
               System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(ba, bb);
    }
}
