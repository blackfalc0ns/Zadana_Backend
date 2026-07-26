using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Domain.Modules.Identity.Entities;

/// <summary>
/// Holds signup data + OTP until email verification succeeds.
/// Email/phone are not written to AspNetUsers until this record is completed.
/// </summary>
public class PendingRegistration
{
    public const int MaxOtpAttempts = 5;
    public static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(24);
    public static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan ResendCooldown = TimeSpan.FromMinutes(1);

    public Guid Id { get; private set; }
    public string Email { get; private set; } = null!;
    public string PhoneNumber { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public string FullName { get; private set; } = null!;
    public UserRole Role { get; private set; }
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
        string phoneNumber,
        string passwordHash,
        string fullName,
        UserRole role,
        string payloadJson,
        string? profilePhotoUrl = null)
    {
        Id = Guid.NewGuid();
        Email = email.Trim().ToLowerInvariant();
        PhoneNumber = phoneNumber.Trim();
        PasswordHash = passwordHash;
        FullName = fullName.Trim();
        Role = role;
        PayloadJson = payloadJson;
        ProfilePhotoUrl = string.IsNullOrWhiteSpace(profilePhotoUrl) ? null : profilePhotoUrl.Trim();
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
        ExpiresAtUtc = CreatedAtUtc.Add(DefaultTtl);
    }

    public bool IsExpired() => DateTime.UtcNow > ExpiresAtUtc;

    public void ReplaceSignupData(
        string passwordHash,
        string fullName,
        string payloadJson,
        string? profilePhotoUrl)
    {
        PasswordHash = passwordHash;
        FullName = fullName.Trim();
        PayloadJson = payloadJson;
        ProfilePhotoUrl = string.IsNullOrWhiteSpace(profilePhotoUrl) ? null : profilePhotoUrl.Trim();
        ExpiresAtUtc = DateTime.UtcNow.Add(DefaultTtl);
        UpdatedAtUtc = DateTime.UtcNow;
    }

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
