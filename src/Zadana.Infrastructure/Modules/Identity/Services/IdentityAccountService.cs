using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Domain.Modules.Identity.Entities;

namespace Zadana.Infrastructure.Modules.Identity.Services;

public class IdentityAccountService : IIdentityAccountService
{
    private readonly UserManager<User> _userManager;

    public IdentityAccountService(UserManager<User> userManager)
    {
        _userManager = userManager;
    }

    public async Task<IdentityAccountSnapshot?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        return user == null ? null : Map(user);
    }

    public async Task<IdentityAccountSnapshot?> FindByIdentifierAsync(string identifier, CancellationToken cancellationToken = default)
    {
        var user = await FindUserByIdentifierAsync(identifier, cancellationToken);
        return user == null ? null : Map(user);
    }

    public async Task<bool> ExistsByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _userManager.Users.AnyAsync(u => u.Id == userId, cancellationToken);

    public async Task<bool> ExistsByEmailOrPhoneAsync(string email, string phoneNumber, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var normalizedPhone = phoneNumber.Trim();

        return await _userManager.Users.AnyAsync(
            u => u.Email == normalizedEmail || u.PhoneNumber == normalizedPhone,
            cancellationToken);
    }

    public async Task<IdentityCreateResult> CreateAsync(CreateIdentityAccountRequest request, CancellationToken cancellationToken = default)
    {
        if (await ExistsByEmailOrPhoneAsync(request.Email, request.PhoneNumber, cancellationToken))
        {
            return new IdentityCreateResult(IdentityCreateStatus.DuplicateEmailOrPhone);
        }

        var user = new User(
            request.FullName,
            request.Email,
            request.PhoneNumber,
            request.Role,
            request.ProfilePhotoUrl);

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return new IdentityCreateResult(
                IdentityCreateStatus.Failed,
                Errors: result.Errors.Select(error => error.Description).ToArray());
        }

        return new IdentityCreateResult(IdentityCreateStatus.Succeeded, Map(user));
    }

    public async Task<IdentityOperationResult> DeleteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return new IdentityOperationResult(true);
        }

        var result = await _userManager.DeleteAsync(user);
        if (result.Succeeded)
        {
            return new IdentityOperationResult(true);
        }

        return new IdentityOperationResult(
            false,
            result.Errors.Select(error => error.Description).ToArray());
    }

    public async Task<CredentialValidationResult> ValidateCredentialsAsync(string identifier, string password, CancellationToken cancellationToken = default)
    {
        var user = await FindUserByIdentifierAsync(identifier, cancellationToken);
        if (user == null)
        {
            return new CredentialValidationResult(CredentialValidationStatus.UserNotFound);
        }

        var isValidPassword = await _userManager.CheckPasswordAsync(user, password);
        if (!isValidPassword)
        {
            return new CredentialValidationResult(CredentialValidationStatus.InvalidPassword);
        }

        return new CredentialValidationResult(CredentialValidationStatus.Succeeded, Map(user));
    }

    public async Task<IdentityOperationResult> RecordLoginAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return new IdentityOperationResult(false, ["User account was not found."]);
        }

        user.RecordLogin();
        return await PersistUserAsync(user);
    }

    public async Task<IdentityOperationResult> RecordActivityAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return new IdentityOperationResult(false, ["User account was not found."]);
        }

        // Avoid a write on every foreground ping while still keeping admin-side activity fresh.
        if (user.LastLoginAtUtc.HasValue && user.LastLoginAtUtc.Value >= DateTime.UtcNow.AddMinutes(-2))
        {
            return new IdentityOperationResult(true);
        }

        user.RecordActivity();
        return await PersistUserAsync(user);
    }

    public async Task<IdentityOperationResult> UpdateProfileAsync(
        Guid userId,
        string fullName,
        string email,
        string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return new IdentityOperationResult(false, ["User account was not found."]);
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var normalizedPhone = phoneNumber.Trim();

        var duplicateExists = await _userManager.Users.AnyAsync(
            candidate => candidate.Id != userId
                && (candidate.Email == normalizedEmail || candidate.PhoneNumber == normalizedPhone),
            cancellationToken);

        if (duplicateExists)
        {
            return new IdentityOperationResult(false, ["Email or phone number is already in use."]);
        }

        user.UpdateProfile(fullName, email, phoneNumber);
        return await PersistUserAsync(user);
    }

    public async Task<IdentityOperationResult> ActivateAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return new IdentityOperationResult(false, ["User account was not found."]);
        }

        user.Activate();
        return await PersistUserAsync(user);
    }

    public async Task<IdentityOperationResult> SuspendAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return new IdentityOperationResult(false, ["User account was not found."]);
        }

        user.Suspend();
        return await PersistUserAsync(user);
    }

    public async Task<IdentityOperationResult> LockLoginAsync(Guid userId, string reason, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return new IdentityOperationResult(false, ["User account was not found."]);
        }

        user.LockLogin(reason);
        return await PersistUserAsync(user);
    }

    public async Task<IdentityOperationResult> UnlockLoginAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return new IdentityOperationResult(false, ["User account was not found."]);
        }

        user.UnlockLogin();
        return await PersistUserAsync(user);
    }

    public async Task<IdentityOperationResult> ArchiveAsync(Guid userId, string reason, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return new IdentityOperationResult(false, ["User account was not found."]);
        }

        user.Archive(reason);
        return await PersistUserAsync(user);
    }

    public async Task<IdentityOperationResult> ResetPasswordByAdminAsync(
        Guid userId,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return new IdentityOperationResult(false, ["User account was not found."]);
        }

        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, resetToken, newPassword);
        if (result.Succeeded)
        {
            return new IdentityOperationResult(true);
        }

        return new IdentityOperationResult(
            false,
            result.Errors.Select(error => error.Description).ToArray());
    }

    public async Task<OtpDispatchResult> GenerateRegistrationOtpAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return new OtpDispatchResult(OtpDispatchStatus.UserNotFound);
        }

        var otpCode = user.GenerateOtp();
        var updateResult = await PersistUserAsync(user);
        if (!updateResult.Succeeded)
        {
            return new OtpDispatchResult(OtpDispatchStatus.Failed, Errors: updateResult.Errors);
        }

        return new OtpDispatchResult(OtpDispatchStatus.Succeeded, Map(user), otpCode);
    }

    public async Task<OtpDispatchResult> ResendRegistrationOtpAsync(string identifier, CancellationToken cancellationToken = default)
    {
        var user = await FindUserByIdentifierAsync(identifier, cancellationToken);
        if (user == null)
        {
            return new OtpDispatchResult(OtpDispatchStatus.UserNotFound);
        }

        if (!user.CanResendOtp())
        {
            var secondsRemaining = Math.Max(0, 60 - (int)(DateTime.UtcNow - user.LastOtpSentAt!.Value).TotalSeconds);
            return new OtpDispatchResult(
                OtpDispatchStatus.CooldownActive,
                Map(user),
                CooldownSecondsRemaining: secondsRemaining);
        }

        var otpCode = user.GenerateOtp();
        var updateResult = await PersistUserAsync(user);
        if (!updateResult.Succeeded)
        {
            return new OtpDispatchResult(OtpDispatchStatus.Failed, Errors: updateResult.Errors);
        }

        return new OtpDispatchResult(OtpDispatchStatus.Succeeded, Map(user), otpCode);
    }

    public async Task<OtpVerificationResult> VerifyRegistrationOtpAsync(string identifier, string otpCode, CancellationToken cancellationToken = default)
    {
        var user = await FindUserByIdentifierAsync(identifier, cancellationToken);
        if (user == null)
        {
            return new OtpVerificationResult(OtpVerificationStatus.UserNotFound);
        }

        if (!user.VerifyOtp(otpCode))
        {
            return new OtpVerificationResult(OtpVerificationStatus.InvalidOrExpiredOtp);
        }

        user.VerifyEmail();
        var updateResult = await PersistUserAsync(user);
        if (!updateResult.Succeeded)
        {
            return new OtpVerificationResult(OtpVerificationStatus.Failed, Errors: updateResult.Errors);
        }

        return new OtpVerificationResult(OtpVerificationStatus.Succeeded, Map(user));
    }

    public async Task<OtpDispatchResult> GeneratePasswordResetOtpAsync(string identifier, CancellationToken cancellationToken = default)
    {
        var user = await FindUserByIdentifierAsync(identifier, cancellationToken);
        if (user == null)
        {
            return new OtpDispatchResult(OtpDispatchStatus.UserNotFound);
        }

        if (!user.CanResendOtp())
        {
            var secondsRemaining = Math.Max(0, 60 - (int)(DateTime.UtcNow - user.LastOtpSentAt!.Value).TotalSeconds);
            return new OtpDispatchResult(
                OtpDispatchStatus.CooldownActive,
                Map(user),
                CooldownSecondsRemaining: secondsRemaining);
        }

        var otpCode = user.GeneratePasswordResetOtp();
        var updateResult = await PersistUserAsync(user);
        if (!updateResult.Succeeded)
        {
            return new OtpDispatchResult(OtpDispatchStatus.Failed, Errors: updateResult.Errors);
        }

        return new OtpDispatchResult(OtpDispatchStatus.Succeeded, Map(user), otpCode);
    }

    public async Task<PasswordResetResult> ResetPasswordAsync(string identifier, string otpCode, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await FindUserByIdentifierAsync(identifier, cancellationToken);
        if (user == null)
        {
            return new PasswordResetResult(PasswordResetStatus.UserNotFound);
        }

        if (!user.VerifyPasswordResetOtp(otpCode))
        {
            return new PasswordResetResult(PasswordResetStatus.InvalidOrExpiredOtp);
        }

        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, resetToken, newPassword);

        if (!result.Succeeded)
        {
            return new PasswordResetResult(
                PasswordResetStatus.Failed,
                result.Errors.Select(error => error.Description).ToArray());
        }

        return new PasswordResetResult(PasswordResetStatus.Succeeded);
    }

    private async Task<User?> FindUserByIdentifierAsync(string identifier, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return null;
        }

        var normalizedIdentifier = identifier.Trim();

        var user = await _userManager.FindByEmailAsync(normalizedIdentifier);
        if (user != null)
        {
            return user;
        }

        return await _userManager.Users.FirstOrDefaultAsync(
            candidate => candidate.PhoneNumber == normalizedIdentifier,
            cancellationToken);
    }

    private async Task<IdentityOperationResult> PersistUserAsync(User user)
    {
        var result = await _userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            return new IdentityOperationResult(true);
        }

        return new IdentityOperationResult(
            false,
            result.Errors.Select(error => error.Description).ToArray());
    }

    private static IdentityAccountSnapshot Map(User user) =>
        new(
            user.Id,
            user.FullName,
            user.Email,
            user.PhoneNumber,
            user.Role,
            user.AccountStatus,
            user.IsLoginLocked,
            user.LockedAtUtc,
            user.ArchivedAtUtc,
            user.EmailConfirmed,
            user.PhoneNumberConfirmed);
}
