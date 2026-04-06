using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.DTOs;
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
            throw new BusinessRuleException("EMAIL_REQUIRED", _localizer["RequiredField", _localizer["Email"].Value]);
        }

        var otpResult = await _identityAccountService.ResendRegistrationOtpAsync(request.Identifier, cancellationToken);
        if (otpResult.Status == OtpDispatchStatus.UserNotFound)
        {
            throw new BusinessRuleException("USER_NOT_FOUND", _localizer["USER_NOT_FOUND", request.Identifier]);
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
        if (!string.IsNullOrWhiteSpace(user.Email) && !string.IsNullOrWhiteSpace(otpResult.OtpCode))
        {
            await _otpService.SendOtpEmailAsync(user.Email, otpResult.OtpCode, cancellationToken);
        }

        var userDto = new CurrentUserDto(user.Id, user.FullName, user.Email, user.PhoneNumber, user.Role.ToString());
        return new AuthResponseDto(null, userDto, false, _localizer["OtpResentSuccessfully"]);
    }
}
