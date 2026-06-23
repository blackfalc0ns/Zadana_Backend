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
    private readonly IOtpService _otpService;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public ResendOtpCommandHandler(
        IIdentityAccountService identityAccountService,
        IOtpService otpService,
        IStringLocalizer<SharedResource> localizer)
    {
        _identityAccountService = identityAccountService;
        _otpService = otpService;
        _localizer = localizer;
    }

    public async Task<AuthResponseDto> Handle(ResendOtpCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Identifier))
        {
            throw new BusinessRuleException("EMAIL_REQUIRED", _localizer["RequiredField", _localizer["Identifier"].Value]);
        }

        return request.Purpose switch
        {
            OtpResendPurpose.PasswordReset => await ResendPasswordResetOtpAsync(request.Identifier, cancellationToken),
            _ => await ResendRegistrationOtpAsync(request.Identifier, cancellationToken)
        };
    }

    private async Task<AuthResponseDto> ResendRegistrationOtpAsync(
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
}
