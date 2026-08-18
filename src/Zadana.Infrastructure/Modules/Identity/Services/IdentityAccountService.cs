using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Enums;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;

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
        return user == null ? null : await MapAsync(user);
    }

    public async Task<IdentityAccountSnapshot?> FindByIdentifierAsync(string identifier, CancellationToken cancellationToken = default)
    {
        var user = await FindUserByIdentifierAsync(identifier, cancellationToken);
        return user == null ? null : await MapAsync(user);
    }

    public async Task<bool> ExistsByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _userManager.Users.AnyAsync(u => u.Id == userId, cancellationToken);

    public async Task<bool> ExistsByEmailOrPhoneAsync(string email, string? phoneNumber, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var normalizedPhone = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();

        return await _userManager.Users.AnyAsync(
            u => u.Email == normalizedEmail || (normalizedPhone != null && u.PhoneNumber == normalizedPhone),
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

        var roleResult = await _userManager.AddToRoleAsync(user, request.Role.ToString());
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            return new IdentityCreateResult(
                IdentityCreateStatus.Failed,
                Errors: roleResult.Errors.Select(error => error.Description).ToArray());
        }

        return new IdentityCreateResult(IdentityCreateStatus.Succeeded, await MapAsync(user));
    }

    public async Task<IdentityCreateResult> CreateWithPasswordHashAsync(
        CreateIdentityAccountRequest request,
        string passwordHash,
        bool emailConfirmed = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            return new IdentityCreateResult(IdentityCreateStatus.Failed, Errors: ["PASSWORD_HASH_REQUIRED"]);
        }

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

        var result = await _userManager.CreateAsync(user);
        if (!result.Succeeded)
        {
            return new IdentityCreateResult(
                IdentityCreateStatus.Failed,
                Errors: result.Errors.Select(error => error.Description).ToArray());
        }

        user.PasswordHash = passwordHash;
        if (emailConfirmed)
        {
            user.VerifyEmail();
        }

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            return new IdentityCreateResult(
                IdentityCreateStatus.Failed,
                Errors: updateResult.Errors.Select(error => error.Description).ToArray());
        }

        var roleResult = await _userManager.AddToRoleAsync(user, request.Role.ToString());
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            return new IdentityCreateResult(
                IdentityCreateStatus.Failed,
                Errors: roleResult.Errors.Select(error => error.Description).ToArray());
        }

        return new IdentityCreateResult(IdentityCreateStatus.Succeeded, await MapAsync(user));
    }

    public async Task<IdentityOperationResult> ConfirmEmailAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return new IdentityOperationResult(false, ["USER_NOT_FOUND"]);
        }

        if (!user.EmailConfirmed)
        {
            user.VerifyEmail();
            var updateResult = await PersistUserAsync(user);
            if (!updateResult.Succeeded)
            {
                return new IdentityOperationResult(false, updateResult.Errors);
            }
        }

        return new IdentityOperationResult(true, Account: await MapAsync(user));
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

        if (await _userManager.IsLockedOutAsync(user))
        {
            return new CredentialValidationResult(CredentialValidationStatus.InvalidPassword);
        }

        var isValidPassword = await _userManager.CheckPasswordAsync(user, password);
        if (!isValidPassword)
        {
            await _userManager.AccessFailedAsync(user);
            return new CredentialValidationResult(CredentialValidationStatus.InvalidPassword);
        }

        if (user.AccessFailedCount > 0)
        {
            await _userManager.ResetAccessFailedCountAsync(user);
        }

        return new CredentialValidationResult(CredentialValidationStatus.Succeeded, await MapAsync(user));
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
        var emailChanged = !string.Equals(user.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase);

        var duplicateExists = await _userManager.Users.AnyAsync(
            candidate => candidate.Id != userId
                && (candidate.Email == normalizedEmail || candidate.PhoneNumber == normalizedPhone),
            cancellationToken);

        if (duplicateExists)
        {
            return new IdentityOperationResult(false, ["Email or phone number is already in use."]);
        }

        user.UpdateProfile(fullName, email, phoneNumber);
        var updateResult = await PersistUserAsync(user);
        return updateResult.Succeeded
            ? new IdentityOperationResult(true, Account: await MapAsync(user), EmailChanged: emailChanged)
            : updateResult;
    }

    public async Task<IdentityOperationResult> UpdateProfilePhotoAsync(
        Guid userId,
        string? profilePhotoUrl,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return new IdentityOperationResult(false, ["User account was not found."]);
        }

        user.UpdateProfilePhoto(profilePhotoUrl);
        return await PersistUserAsync(user);
    }

    public async Task<IdentityOperationResult> UpdateRoleAsync(
        Guid userId,
        UserRole role,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return new IdentityOperationResult(false, ["User account was not found."]);
        }

        var targetRole = role.ToString();
        var currentRoles = await _userManager.GetRolesAsync(user);

        if (!currentRoles.Contains(targetRole, StringComparer.OrdinalIgnoreCase))
        {
            var addResult = await _userManager.AddToRoleAsync(user, targetRole);
            if (!addResult.Succeeded)
            {
                return new IdentityOperationResult(
                    false,
                    addResult.Errors.Select(error => error.Description).ToArray());
            }
        }

        var identityRoleNames = Enum.GetNames<UserRole>();
        var rolesToRemove = currentRoles
            .Where(current => !current.Equals(targetRole, StringComparison.OrdinalIgnoreCase)
                && identityRoleNames.Contains(current, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        if (rolesToRemove.Length > 0)
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
            if (!removeResult.Succeeded)
            {
                return new IdentityOperationResult(
                    false,
                    removeResult.Errors.Select(error => error.Description).ToArray());
            }
        }

        user.UpdateRole(role);
        return await PersistUserAsync(user);
    }

    public async Task<IdentityOperationResult> AddPlatformRoleAsync(
        Guid userId,
        UserRole role,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return new IdentityOperationResult(false, ["User account was not found."]);
        }

        var targetRole = role.ToString();
        if (!await _userManager.IsInRoleAsync(user, targetRole))
        {
            var addResult = await _userManager.AddToRoleAsync(user, targetRole);
            if (!addResult.Succeeded)
            {
                return new IdentityOperationResult(
                    false,
                    addResult.Errors.Select(error => error.Description).ToArray());
            }
        }

        return new IdentityOperationResult(true, Account: await MapAsync(user));
    }

    public async Task<IdentityOperationResult> RemovePlatformRoleAsync(
        Guid userId,
        UserRole role,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return new IdentityOperationResult(false, ["User account was not found."]);
        }

        var targetRole = role.ToString();
        if (await _userManager.IsInRoleAsync(user, targetRole))
        {
            var removeResult = await _userManager.RemoveFromRoleAsync(user, targetRole);
            if (!removeResult.Succeeded)
            {
                return new IdentityOperationResult(
                    false,
                    removeResult.Errors.Select(error => error.Description).ToArray());
            }
        }

        if (user.Role == role)
        {
            var remaining = await _userManager.GetRolesAsync(user);
            UserRole? nextPrimary = null;
            foreach (var name in remaining)
            {
                if (Enum.TryParse<UserRole>(name, true, out var parsed))
                {
                    nextPrimary = parsed;
                    break;
                }
            }

            if (nextPrimary.HasValue)
            {
                user.UpdateRole(nextPrimary.Value);
                var persistPrimary = await PersistUserAsync(user);
                if (!persistPrimary.Succeeded)
                {
                    return persistPrimary;
                }
            }
        }

        return new IdentityOperationResult(true, Account: await MapAsync(user));
    }

    public async Task<IdentityOperationResult> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return new IdentityOperationResult(false, ["User account was not found."]);
        }

        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
        {
            return new IdentityOperationResult(
                false,
                result.Errors.Select(error => error.Description).ToArray());
        }

        user.CompletePasswordChange();
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

    public async Task<bool> CheckPasswordAsync(Guid userId, string password, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null || string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        return await _userManager.CheckPasswordAsync(user, password);
    }

    public async Task<IdentityOperationResult> AnonymizeClosedAccountAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return new IdentityOperationResult(false, ["User account was not found."]);
        }

        user.AnonymizeForClosure();
        user.IncrementPermissionVersion();
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

        var policyErrors = await ValidatePasswordPolicyAsync(user, newPassword);
        if (policyErrors.Length > 0)
        {
            return new IdentityOperationResult(false, policyErrors);
        }

        var setResult = await SetPasswordAfterOtpVerificationAsync(user, newPassword);
        if (!setResult.Succeeded)
        {
            return setResult;
        }

        user.CompletePasswordChange();
        return await PersistUserAsync(user);
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

        return new OtpDispatchResult(OtpDispatchStatus.Succeeded, await MapAsync(user), otpCode);
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
                await MapAsync(user),
                CooldownSecondsRemaining: secondsRemaining);
        }

        var otpCode = user.GenerateOtp();
        var updateResult = await PersistUserAsync(user);
        if (!updateResult.Succeeded)
        {
            return new OtpDispatchResult(OtpDispatchStatus.Failed, Errors: updateResult.Errors);
        }

        return new OtpDispatchResult(OtpDispatchStatus.Succeeded, await MapAsync(user), otpCode);
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

        return new OtpVerificationResult(OtpVerificationStatus.Succeeded, await MapAsync(user));
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
                await MapAsync(user),
                CooldownSecondsRemaining: secondsRemaining);
        }

        var otpCode = user.GeneratePasswordResetOtp();
        var updateResult = await PersistUserAsync(user);
        if (!updateResult.Succeeded)
        {
            return new OtpDispatchResult(OtpDispatchStatus.Failed, Errors: updateResult.Errors);
        }

        return new OtpDispatchResult(OtpDispatchStatus.Succeeded, await MapAsync(user), otpCode);
    }

    public async Task<OtpResendPurpose> ResolveOtpResendPurposeAsync(
        string identifier,
        OtpResendPurpose requestedPurpose,
        bool purposeExplicitlyProvided,
        CancellationToken cancellationToken = default)
    {
        if (purposeExplicitlyProvided || requestedPurpose == OtpResendPurpose.PasswordReset)
        {
            return requestedPurpose;
        }

        var user = await FindUserByIdentifierAsync(identifier, cancellationToken);
        if (user == null)
        {
            return requestedPurpose;
        }

        if (user.HasPendingRegistrationVerification())
        {
            return OtpResendPurpose.Registration;
        }

        return OtpResendPurpose.PasswordReset;
    }

    public async Task<PasswordResetOtpVerificationResult> VerifyPasswordResetOtpAsync(
        string identifier,
        string otpCode,
        CancellationToken cancellationToken = default)
    {
        var user = await FindUserByIdentifierAsync(identifier, cancellationToken);
        if (user == null)
        {
            return new PasswordResetOtpVerificationResult(PasswordResetOtpVerificationStatus.InvalidOrExpiredOtp);
        }

        var resetToken = user.ConfirmPasswordResetOtp(otpCode);
        if (resetToken == null)
        {
            return new PasswordResetOtpVerificationResult(PasswordResetOtpVerificationStatus.InvalidOrExpiredOtp);
        }

        var persistResult = await PersistUserAsync(user);
        if (!persistResult.Succeeded)
        {
            return new PasswordResetOtpVerificationResult(
                PasswordResetOtpVerificationStatus.Failed,
                Errors: persistResult.Errors);
        }

        return new PasswordResetOtpVerificationResult(
            PasswordResetOtpVerificationStatus.Succeeded,
            ResetToken: resetToken,
            ExpiresInSeconds: User.PasswordResetProofLifetimeMinutes * 60);
    }

    public async Task<PasswordResetResult> CompletePasswordResetAsync(
        string identifier,
        string resetToken,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await FindUserByIdentifierAsync(identifier, cancellationToken);
        if (user == null)
        {
            return new PasswordResetResult(PasswordResetStatus.UserNotFound);
        }

        if (!user.ValidatePasswordResetProof(resetToken))
        {
            return new PasswordResetResult(PasswordResetStatus.InvalidOrExpiredResetToken);
        }

        var passwordResult = await SetPasswordAfterOtpVerificationAsync(user, newPassword);
        if (!passwordResult.Succeeded)
        {
            return new PasswordResetResult(
                PasswordResetStatus.Failed,
                passwordResult.Errors);
        }

        user = await FindUserByIdentifierAsync(identifier, cancellationToken);
        if (user == null)
        {
            return new PasswordResetResult(PasswordResetStatus.UserNotFound);
        }

        user.ClearPasswordResetSession();
        user.CompletePasswordChange();
        var persistResult = await PersistUserAsync(user);
        if (!persistResult.Succeeded)
        {
            return new PasswordResetResult(
                PasswordResetStatus.Failed,
                persistResult.Errors);
        }

        return new PasswordResetResult(PasswordResetStatus.Succeeded);
    }

    private async Task<IdentityOperationResult> SetPasswordAfterOtpVerificationAsync(User user, string newPassword)
    {
        IdentityResult result;
        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            result = await _userManager.AddPasswordAsync(user, newPassword);
        }
        else
        {
            var removeResult = await _userManager.RemovePasswordAsync(user);
            if (!removeResult.Succeeded)
            {
                return new IdentityOperationResult(false, MapIdentityErrors(removeResult.Errors));
            }

            result = await _userManager.AddPasswordAsync(user, newPassword);
        }

        if (!result.Succeeded)
        {
            return new IdentityOperationResult(false, MapIdentityErrors(result.Errors));
        }

        return new IdentityOperationResult(true);
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
            MapIdentityErrors(result.Errors));
    }

    private async Task<string[]> ValidatePasswordPolicyAsync(User user, string newPassword)
    {
        var errors = new List<IdentityError>();
        foreach (var validator in _userManager.PasswordValidators)
        {
            var result = await validator.ValidateAsync(_userManager, user, newPassword);
            if (!result.Succeeded)
            {
                errors.AddRange(result.Errors);
            }
        }

        return MapIdentityErrors(errors);
    }

    private static string[] MapIdentityErrors(IEnumerable<IdentityError> errors) =>
        errors.Select(error =>
            error.Code switch
            {
                "PasswordTooShort" =>
                    "كلمة المرور لازم 8 أحرف على الأقل.|Password must be at least 8 characters.",
                "PasswordRequiresDigit" =>
                    "كلمة المرور لازم فيها رقم.|Password must include a number.",
                "PasswordRequiresLower" =>
                    "كلمة المرور لازم فيها حرف إنجليزي صغير.|Password must include a lowercase letter.",
                "PasswordRequiresUpper" =>
                    "كلمة المرور لازم فيها حرف إنجليزي كبير.|Password must include an uppercase letter.",
                "PasswordRequiresNonAlphanumeric" =>
                    "كلمة المرور لازم فيها رمز.|Password must include a symbol.",
                _ => string.IsNullOrWhiteSpace(error.Code)
                    ? error.Description
                    : $"{error.Code}: {error.Description}"
            }).ToArray();

    private async Task<IdentityAccountSnapshot> MapAsync(User user)
    {
        var identityRoles = await _userManager.GetRolesAsync(user);
        var platformRoles = new HashSet<UserRole> { user.Role };
        foreach (var name in identityRoles)
        {
            if (Enum.TryParse<UserRole>(name, true, out var parsed))
            {
                platformRoles.Add(parsed);
            }
        }

        return new IdentityAccountSnapshot(
            user.Id,
            user.FullName,
            user.Email,
            user.PhoneNumber,
            user.Role,
            user.PermissionVersion,
            user.AccountStatus,
            user.IsLoginLocked,
            user.LockedAtUtc,
            user.ArchivedAtUtc,
            user.EmailConfirmed,
            user.PhoneNumberConfirmed,
            user.MustChangePassword,
            user.ProfilePhotoUrl,
            platformRoles.ToArray());
    }
}
