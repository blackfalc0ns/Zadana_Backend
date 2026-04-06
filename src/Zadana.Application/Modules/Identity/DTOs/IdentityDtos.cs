using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Application.Modules.Identity.DTOs;

public record TokenPairDto(string AccessToken, string RefreshToken);

public record CurrentUserDto(Guid Id, string FullName, string? Email, string? Phone, string Role);

public record AuthResponseDto(TokenPairDto? Tokens, CurrentUserDto? User, bool IsVerified = true, string? Message = null);

public record IdentityAccountSnapshot(
    Guid Id,
    string FullName,
    string? Email,
    string? PhoneNumber,
    UserRole Role,
    AccountStatus AccountStatus,
    bool EmailConfirmed,
    bool PhoneNumberConfirmed);

public record CreateIdentityAccountRequest(
    string FullName,
    string Email,
    string PhoneNumber,
    UserRole Role,
    string Password,
    string? ProfilePhotoUrl = null);

public enum IdentityCreateStatus
{
    Succeeded,
    DuplicateEmailOrPhone,
    Failed
}

public record IdentityCreateResult(
    IdentityCreateStatus Status,
    IdentityAccountSnapshot? Account = null,
    IReadOnlyCollection<string>? Errors = null);

public enum CredentialValidationStatus
{
    Succeeded,
    UserNotFound,
    InvalidPassword
}

public record CredentialValidationResult(
    CredentialValidationStatus Status,
    IdentityAccountSnapshot? Account = null);

public record IdentityOperationResult(
    bool Succeeded,
    IReadOnlyCollection<string>? Errors = null);

public enum OtpDispatchStatus
{
    Succeeded,
    UserNotFound,
    CooldownActive,
    Failed
}

public record OtpDispatchResult(
    OtpDispatchStatus Status,
    IdentityAccountSnapshot? Account = null,
    string? OtpCode = null,
    int? CooldownSecondsRemaining = null,
    IReadOnlyCollection<string>? Errors = null);

public enum OtpVerificationStatus
{
    Succeeded,
    UserNotFound,
    InvalidOrExpiredOtp,
    Failed
}

public record OtpVerificationResult(
    OtpVerificationStatus Status,
    IdentityAccountSnapshot? Account = null,
    IReadOnlyCollection<string>? Errors = null);

public enum PasswordResetStatus
{
    Succeeded,
    UserNotFound,
    InvalidOrExpiredOtp,
    Failed
}

public record PasswordResetResult(
    PasswordResetStatus Status,
    IReadOnlyCollection<string>? Errors = null);

public record RefreshTokenRecord(
    Guid UserId,
    string Token,
    DateTime ExpiresAtUtc,
    bool IsRevoked,
    DateTime? RevokedAtUtc,
    IdentityAccountSnapshot? User = null)
{
    public bool IsExpired => DateTime.UtcNow >= ExpiresAtUtc;
    public bool IsActive => !IsRevoked && !IsExpired;
}

public record NewRefreshToken(Guid UserId, string Token, DateTime ExpiresAtUtc);
