using Zadana.Application.Modules.Identity.DTOs;

namespace Zadana.Application.Modules.Identity.Interfaces;

public interface IIdentityAccountService
{
    Task<IdentityAccountSnapshot?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IdentityAccountSnapshot?> FindByIdentifierAsync(string identifier, CancellationToken cancellationToken = default);
    Task<bool> ExistsByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByEmailOrPhoneAsync(string email, string phoneNumber, CancellationToken cancellationToken = default);
    Task<IdentityCreateResult> CreateAsync(CreateIdentityAccountRequest request, CancellationToken cancellationToken = default);
    Task<IdentityOperationResult> DeleteAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<CredentialValidationResult> ValidateCredentialsAsync(string identifier, string password, CancellationToken cancellationToken = default);
    Task<IdentityOperationResult> RecordLoginAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IdentityOperationResult> UpdateProfileAsync(Guid userId, string fullName, string email, string phoneNumber, CancellationToken cancellationToken = default);
    Task<IdentityOperationResult> ActivateAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IdentityOperationResult> SuspendAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IdentityOperationResult> LockLoginAsync(Guid userId, string reason, CancellationToken cancellationToken = default);
    Task<IdentityOperationResult> UnlockLoginAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IdentityOperationResult> ArchiveAsync(Guid userId, string reason, CancellationToken cancellationToken = default);
    Task<IdentityOperationResult> ResetPasswordByAdminAsync(Guid userId, string newPassword, CancellationToken cancellationToken = default);
    Task<OtpDispatchResult> GenerateRegistrationOtpAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<OtpDispatchResult> ResendRegistrationOtpAsync(string identifier, CancellationToken cancellationToken = default);
    Task<OtpVerificationResult> VerifyRegistrationOtpAsync(string identifier, string otpCode, CancellationToken cancellationToken = default);
    Task<OtpDispatchResult> GeneratePasswordResetOtpAsync(string identifier, CancellationToken cancellationToken = default);
    Task<PasswordResetResult> ResetPasswordAsync(string identifier, string otpCode, string newPassword, CancellationToken cancellationToken = default);
}
