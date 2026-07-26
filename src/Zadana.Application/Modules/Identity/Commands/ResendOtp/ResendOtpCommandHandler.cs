using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Enums;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.SharedKernel.Exceptions;
using Zadana.Application.Common.Localization;
using Microsoft.Extensions.Localization;

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

        var resolvedPurpose = await _identityAccountService.ResolveOtpResendPurposeAsync(
            request.Identifier,
            request.Purpose,
            request.PurposeExplicitlyProvided,
            cancellationToken);

        return resolvedPurpose switch
        {
            OtpResendPurpose.PasswordReset => await ResendPasswordResetOtpAsync(request.Identifier, cancellationToken),
            _ => await ResendRegistrationOtpAsync(request.Identifier, cancellationToken)
        };
    }

    private async Task<AuthResponseDto> ResendRegistrationOtpAsync(
        string identifier,
        CancellationToken cancellationToken)
    {
        var pendingResult = await _pendingRegistrationService.ResendOtpAsync(identifier, cancellationToken: cancellationToken);
        if (pendingResult.Status == PendingOtpDispatchStatus.Succeeded &&
            pendingResult.Pending is not null &&
            !string.IsNullOrWhiteSpace(pendingResult.PlainOtpCode))
        {
            await SendRegistrationOtpEmailAsync(pendingResult.Pending.Email, pendingResult.PlainOtpCode, cancellationToken);
            var pendingUserDto = new CurrentUserDto(
                pendingResult.Pending.Id,
                pendingResult.Pending.FullName,
                pendingResult.Pending.Email,
                pendingResult.Pending.PhoneNumber,
                pendingResult.Pending.Role.ToString(),
                MustChangePassword: false,
                ProfilePhotoUrl: pendingResult.Pending.ProfilePhotoUrl);
            return new AuthResponseDto(null, pendingUserDto, false, _localizer["OtpResentSuccessfully"]);
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
}
