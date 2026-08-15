using MediatR;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Enums;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Application.Modules.Identity.Support;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Identity.Commands.ResendOtp;

public class ResendOtpCommandHandler : IRequestHandler<ResendOtpCommand, AuthResponseDto>
{
    private readonly IIdentityAccountService _identityAccountService;
    private readonly IPendingRegistrationService _pendingRegistrationService;
    private readonly IOtpService _otpService;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public ResendOtpCommandHandler(
        IIdentityAccountService identityAccountService,
        IPendingRegistrationService pendingRegistrationService,
        IOtpService otpService,
        IStringLocalizer<SharedResource> localizer)
    {
        _identityAccountService = identityAccountService;
        _pendingRegistrationService = pendingRegistrationService;
        _otpService = otpService;
        _localizer = localizer;
    }

    public async Task<AuthResponseDto> Handle(ResendOtpCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Identifier))
        {
            throw new BusinessRuleException("EMAIL_REQUIRED", _localizer["RequiredField", _localizer["Identifier"].Value]);
        }

        if (request.Purpose != OtpResendPurpose.PasswordReset)
        {
            var pendingResponse = await TryResendPendingRegistrationAsync(request, cancellationToken);
            if (pendingResponse is not null)
            {
                return pendingResponse;
            }
        }

        var resolvedPurpose = await _identityAccountService.ResolveOtpResendPurposeAsync(
            request.Identifier,
            request.Purpose,
            request.PurposeExplicitlyProvided,
            cancellationToken);

        return resolvedPurpose switch
        {
            OtpResendPurpose.PasswordReset => await ResendPasswordResetOtpAsync(request.Identifier, cancellationToken),
            _ => await ResendLegacyRegistrationOtpAsync(request.Identifier, cancellationToken)
        };
    }

    private async Task<AuthResponseDto?> TryResendPendingRegistrationAsync(
        ResendOtpCommand request,
        CancellationToken cancellationToken)
    {
        var pendingResult = await _pendingRegistrationService.ResendOtpAsync(
            request.RegistrationToken,
            request.ExpectedRole,
            request.Identifier,
            cancellationToken);

        if (pendingResult.Status == PendingOtpDispatchStatus.NotFound)
        {
            return null;
        }

        if (pendingResult.Status == PendingOtpDispatchStatus.CooldownActive)
        {
            var pendingCooldown = pendingResult.CooldownSecondsRemaining ?? 0;
            throw new BusinessRuleException("OTP_COOLDOWN", _localizer["OtpCooldown", pendingCooldown]);
        }

        if (pendingResult.Status == PendingOtpDispatchStatus.Expired)
        {
            throw new BusinessRuleException("INVALID_OTP", _localizer["InvalidOrExpiredOtp"]);
        }

        if (pendingResult.Status != PendingOtpDispatchStatus.Succeeded ||
            pendingResult.Pending is null ||
            string.IsNullOrWhiteSpace(pendingResult.PlainOtpCode) ||
            string.IsNullOrWhiteSpace(pendingResult.RegistrationToken))
        {
            return null;
        }

        EnsureIdentifierMatchesPending(request.Identifier, pendingResult.Pending);
        await SendRegistrationOtpEmailAsync(
            pendingResult.Pending.Email,
            pendingResult.PlainOtpCode,
            cancellationToken);
        var pendingUserDto = new CurrentUserDto(
            pendingResult.Pending.Id,
            pendingResult.Pending.FullName,
            pendingResult.Pending.Email,
            pendingResult.Pending.PhoneNumber,
            pendingResult.Pending.Role.ToString(),
            MustChangePassword: false,
            ProfilePhotoUrl: pendingResult.Pending.ProfilePhotoUrl);
        return new AuthResponseDto(
            null,
            pendingUserDto,
            false,
            _localizer["OtpResentSuccessfully"],
            RegistrationToken: pendingResult.RegistrationToken);
    }

    private async Task<AuthResponseDto> ResendLegacyRegistrationOtpAsync(
        string identifier,
        CancellationToken cancellationToken)
    {
        var otpResult = await _identityAccountService.ResendRegistrationOtpAsync(identifier, cancellationToken);
        if (otpResult.Status == OtpDispatchStatus.UserNotFound)
        {
            throw new BusinessRuleException("USER_NOT_FOUND", _localizer["USER_NOT_FOUND", identifier]);
        }

        if (otpResult.Status == OtpDispatchStatus.CooldownActive)
        {
            var timeLeft = otpResult.CooldownSecondsRemaining ?? 0;
            throw new BusinessRuleException("OTP_COOLDOWN", _localizer["OtpCooldown", timeLeft]);
        }

        if (otpResult.Status == OtpDispatchStatus.Failed)
        {
            var errors = string.Join(", ", otpResult.Errors ?? []);
            throw new BusinessRuleException("IDENTITY_OPERATION_FAILED", $"{_localizer["IDENTITY_OPERATION_FAILED"]}: {errors}");
        }

        var user = otpResult.Account!;
        await SendRegistrationOtpEmailAsync(user.Email, otpResult.OtpCode!, cancellationToken);

        var userDto = new CurrentUserDto(
            user.Id,
            user.FullName,
            user.Email,
            user.PhoneNumber,
            user.Role.ToString(),
            user.MustChangePassword,
            ProfilePhotoUrl: user.ProfilePhotoUrl);

        return new AuthResponseDto(null, userDto, false, _localizer["OtpResentSuccessfully"]);
    }

    private async Task<AuthResponseDto> ResendPasswordResetOtpAsync(
        string identifier,
        CancellationToken cancellationToken)
    {
        var otpResult = await _identityAccountService.GeneratePasswordResetOtpAsync(identifier, cancellationToken);

        if (otpResult.Status == OtpDispatchStatus.UserNotFound)
        {
            // Same privacy behavior as forgot-password: do not reveal whether the account exists.
            return new AuthResponseDto(null, null, false, _localizer["PasswordResetOtpSent"]);
        }

        if (otpResult.Status == OtpDispatchStatus.CooldownActive)
        {
            var timeLeft = otpResult.CooldownSecondsRemaining ?? 0;
            throw new BusinessRuleException("OTP_COOLDOWN", _localizer["OtpCooldown", timeLeft]);
        }

        if (otpResult.Status == OtpDispatchStatus.Failed)
        {
            var errors = string.Join(", ", otpResult.Errors ?? []);
            throw new BusinessRuleException("IDENTITY_OPERATION_FAILED", $"{_localizer["IDENTITY_OPERATION_FAILED"]}: {errors}");
        }

        if (otpResult.Account is null || string.IsNullOrWhiteSpace(otpResult.OtpCode))
        {
            return new AuthResponseDto(null, null, false, _localizer["PasswordResetOtpSent"]);
        }

        await SendPasswordResetOtpEmailAsync(otpResult.Account.Email, otpResult.OtpCode, cancellationToken);

        var userDto = new CurrentUserDto(
            otpResult.Account.Id,
            otpResult.Account.FullName,
            otpResult.Account.Email,
            otpResult.Account.PhoneNumber,
            otpResult.Account.Role.ToString(),
            otpResult.Account.MustChangePassword,
            ProfilePhotoUrl: otpResult.Account.ProfilePhotoUrl);

        return new AuthResponseDto(null, userDto, false, _localizer["PasswordResetOtpSent"]);
    }

    private async Task SendRegistrationOtpEmailAsync(
        string? email,
        string otpCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        await _otpService.SendOtpEmailAsync(email, otpCode, cancellationToken);
    }

    private async Task SendPasswordResetOtpEmailAsync(
        string? email,
        string otpCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        await _otpService.SendOtpEmailAsync(email, otpCode, cancellationToken, validityMinutes: 15);
    }

    private static void EnsureIdentifierMatchesPending(string identifier, PendingRegistrationSnapshot pending)
    {
        if (RegistrationContactMatcher.Matches(identifier, pending.Email, pending.PhoneNumber) ||
            RegistrationContactMatcher.Matches(identifier, pending.OtpDestinationEmail, null))
        {
            return;
        }

        throw new BusinessRuleException("USER_NOT_FOUND", "USER_NOT_FOUND");
    }
}
