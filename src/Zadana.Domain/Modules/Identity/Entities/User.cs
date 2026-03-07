using Microsoft.AspNetCore.Identity;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Identity.Entities;

public class User : IdentityUser<Guid>
{
    public string FullName { get; private set; } = null!;
    public UserRole Role { get; private set; }
    public AccountStatus AccountStatus { get; private set; }
    
    public string? OtpCode { get; private set; }
    public DateTime? OtpExpiryTime { get; private set; }
    public string? PasswordResetOtp { get; private set; }
    public DateTime? PasswordResetOtpExpiry { get; private set; }
    public DateTime? LastLoginAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

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
        EmailConfirmed = false;
        PhoneNumberConfirmed = false;
        ProfilePhotoUrl = profilePhotoUrl;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateProfile(string fullName, string phone)
    {
        FullName = fullName.Trim();
        PhoneNumber = phone.Trim();
    }

    public void VerifyEmail() => EmailConfirmed = true;
    public void VerifyPhone() => PhoneNumberConfirmed = true;

    public void RecordLogin() => LastLoginAtUtc = DateTime.UtcNow;

    public void Suspend() => AccountStatus = AccountStatus.Suspended;
    public void Activate() => AccountStatus = AccountStatus.Active;
    public void Ban() => AccountStatus = AccountStatus.Banned;
    public void Deactivate() => AccountStatus = AccountStatus.Inactive;

    // --- OTP Domain Behavior ---
    public string GenerateOtp()
    {
        // Generate a 4-digit code (simple string randomizer)
        var random = new Random();
        OtpCode = random.Next(1000, 9999).ToString();
        OtpExpiryTime = DateTime.UtcNow.AddMinutes(5); // Valid for 5 minutes
        return OtpCode;
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
        
        return true;
    }

    // --- Password Reset Domain Behavior ---
    public string GeneratePasswordResetOtp()
    {
        var random = new Random();
        PasswordResetOtp = random.Next(1000, 9999).ToString();
        PasswordResetOtpExpiry = DateTime.UtcNow.AddMinutes(15); // Valid for 15 minutes
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
        
        return true;
    }
}
