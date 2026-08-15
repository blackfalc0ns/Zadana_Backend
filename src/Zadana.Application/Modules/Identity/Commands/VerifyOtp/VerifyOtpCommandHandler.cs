using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Application.Modules.Identity.Support;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Identity.Commands.VerifyOtp;

public class VerifyOtpCommandHandler : IRequestHandler<VerifyOtpCommand, AuthResponseDto>
{
    private readonly IPendingRegistrationService _pendingRegistrationService;
    private readonly IRegistrationRoleMaterializer _registrationRoleMaterializer;
    private readonly IIdentityAccountService _identityAccountService;
    private readonly IRegistrationWorkflow _registrationWorkflow;
    private readonly IRefreshTokenStore _refreshTokenStore;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IAccessControlService _accessControlService;
    private readonly IApplicationDbContext _context;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public VerifyOtpCommandHandler(
        IPendingRegistrationService pendingRegistrationService,
        IRegistrationRoleMaterializer registrationRoleMaterializer,
        IIdentityAccountService identityAccountService,
        IRegistrationWorkflow registrationWorkflow,
        IRefreshTokenStore refreshTokenStore,
        IUnitOfWork unitOfWork,
        IJwtTokenService jwtTokenService,
        IAccessControlService accessControlService,
        IApplicationDbContext context,
        IStringLocalizer<SharedResource> localizer)
    {
        _pendingRegistrationService = pendingRegistrationService;
        _registrationRoleMaterializer = registrationRoleMaterializer;
        _identityAccountService = identityAccountService;
        _registrationWorkflow = registrationWorkflow;
        _refreshTokenStore = refreshTokenStore;
        _unitOfWork = unitOfWork;
        _jwtTokenService = jwtTokenService;
        _accessControlService = accessControlService;
        _context = context;
        _localizer = localizer;
    }

    public async Task<AuthResponseDto> Handle(VerifyOtpCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Identifier))
        {
            throw new BusinessRuleException("EMAIL_REQUIRED", _localizer["RequiredField", _localizer["Identifier"].Value]);
        }

        var completion = await _pendingRegistrationService.VerifyAndCreateAccountAsync(
            request.RegistrationToken,
            request.OtpCode,
            request.Identifier,
            request.ExpectedRole,
            cancellationToken);

        if (completion.Status != PendingCompletionStatus.NotFound)
        {
            return await CompletePendingRegistrationAsync(request.Identifier, completion, cancellationToken);
        }

        return await CompleteLegacyUserVerificationAsync(request.Identifier, request.OtpCode, cancellationToken);
    }

    private async Task<AuthResponseDto> CompletePendingRegistrationAsync(
        string identifier,
        PendingCompletionResult completion,
        CancellationToken cancellationToken)
    {
        if (completion.Status == PendingCompletionStatus.NotFound)
        {
            throw new BusinessRuleException("USER_NOT_FOUND", _localizer["USER_NOT_FOUND", identifier]);
        }

        if (completion.Status == PendingCompletionStatus.Expired)
        {
            throw new BusinessRuleException("INVALID_OTP", _localizer["InvalidOrExpiredOtp"]);
        }

        if (completion.Status == PendingCompletionStatus.InvalidOtp)
        {
            throw new BusinessRuleException("INVALID_OTP", _localizer["InvalidOrExpiredOtp"]);
        }

        if (completion.Status != PendingCompletionStatus.Succeeded ||
            completion.Account is null ||
            completion.Role is null ||
            string.IsNullOrWhiteSpace(completion.PayloadJson))
        {
            var errors = string.Join(", ", completion.Errors ?? []);
            if (completion.Errors?.Contains("USER_ALREADY_EXISTS") == true)
            {
                throw new BusinessRuleException("USER_ALREADY_EXISTS", _localizer["USER_ALREADY_EXISTS"]);
            }

            throw new BusinessRuleException("VERIFICATION_FAILED", $"{_localizer["VERIFICATION_FAILED"]}: {errors}");
        }

        EnsureIdentifierMatchesAccount(
            identifier,
            completion.Account,
            completion.RegistrationEmail,
            completion.RegistrationPhone);

        try
        {
            await _registrationRoleMaterializer.MaterializeAsync(
                completion.Account,
                completion.Role.Value,
                completion.PayloadJson,
                cancellationToken);
        }
        catch
        {
            if (!completion.LinkedExistingAccount)
            {
                await _registrationWorkflow.CompensateAccountCreationFailureAsync(completion.Account.Id, cancellationToken);
            }

            throw;
        }

        // Materialize may bump permission_version (access scope). Reload so issued JWTs match middleware checks.
        var account = await _identityAccountService.FindByIdAsync(completion.Account.Id, cancellationToken)
            ?? completion.Account;

        return await BuildVerifiedAuthResponseAsync(account, completion.Role.Value, cancellationToken);
    }

    private async Task<AuthResponseDto> CompleteLegacyUserVerificationAsync(
        string identifier,
        string otpCode,
        CancellationToken cancellationToken)
    {
        var verificationResult = await _identityAccountService.VerifyRegistrationOtpAsync(
            identifier,
            otpCode,
            cancellationToken);

        if (verificationResult.Status == OtpVerificationStatus.UserNotFound)
        {
            throw new BusinessRuleException("USER_NOT_FOUND", _localizer["USER_NOT_FOUND", identifier]);
        }

        if (verificationResult.Status == OtpVerificationStatus.Failed)
        {
            var errors = string.Join(", ", verificationResult.Errors ?? []);
            throw new BusinessRuleException("VERIFICATION_FAILED", $"{_localizer["VERIFICATION_FAILED"]}: {errors}");
        }

        if (verificationResult.Status != OtpVerificationStatus.Succeeded || verificationResult.Account == null)
        {
            throw new BusinessRuleException("INVALID_OTP", _localizer["InvalidOrExpiredOtp"]);
        }

        return await BuildVerifiedAuthResponseAsync(verificationResult.Account, verificationResult.Account.Role, cancellationToken);
    }

    private async Task<AuthResponseDto> BuildVerifiedAuthResponseAsync(
        IdentityAccountSnapshot user,
        UserRole sessionRole,
        CancellationToken cancellationToken)
    {
        if (user.IsLoginLocked || user.AccountStatus != AccountStatus.Active)
        {
            throw new UnauthorizedException(_localizer["AccountLoginDenied", user.AccountStatus]);
        }

        var sessionUser = PlatformRoleMembership.WithSessionRole(user, sessionRole);
        var tokens = await _jwtTokenService.GenerateTokenPairAsync(sessionUser, cancellationToken);
        _refreshTokenStore.Add(new NewRefreshToken(sessionUser.Id, tokens.RefreshToken, DateTime.UtcNow.AddDays(7)));
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        DriverOperationalStatusDto? driverStatus = null;
        if (sessionUser.Role == UserRole.Driver)
        {
            var driver = await _context.Drivers
                .FirstOrDefaultAsync(d => d.UserId == sessionUser.Id, cancellationToken);

            if (driver is not null)
            {
                driverStatus = DriverOperationalStatusFactory.Create(
                    driver,
                    isLoginLocked: sessionUser.IsLoginLocked,
                    lockedAtUtc: sessionUser.LockedAtUtc);
            }
        }

        var access = await _accessControlService.GetEffectiveAccessAsync(sessionUser.Id, sessionUser.Role, cancellationToken);
        var userDto = new CurrentUserDto(
            sessionUser.Id,
            sessionUser.FullName,
            sessionUser.Email,
            sessionUser.PhoneNumber,
            sessionUser.Role.ToString(),
            sessionUser.MustChangePassword,
            Access: access,
            ProfilePhotoUrl: sessionUser.ProfilePhotoUrl);

        var isVerified = AuthResponseVerificationResolver.Resolve(sessionUser, driverStatus);
        return new AuthResponseDto(
            tokens,
            userDto,
            IsVerified: isVerified,
            Message: _localizer["AccountCreatedSuccessfully"],
            DriverStatus: driverStatus);
    }

    private static void EnsureIdentifierMatchesAccount(
        string identifier,
        IdentityAccountSnapshot account,
        string? registrationEmail,
        string? registrationPhone)
    {
        if (RegistrationContactMatcher.Matches(identifier, account.Email, account.PhoneNumber) ||
            RegistrationContactMatcher.Matches(identifier, registrationEmail, registrationPhone))
        {
            return;
        }

        throw new BusinessRuleException("USER_NOT_FOUND", "USER_NOT_FOUND");
    }
}
