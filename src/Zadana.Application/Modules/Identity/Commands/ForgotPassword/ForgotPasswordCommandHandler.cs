using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.SharedKernel.Exceptions;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Identity.Commands.ForgotPassword;

public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand>
{
    private readonly IIdentityAccountService _identityAccountService;
    private readonly IOtpService _otpService;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public ForgotPasswordCommandHandler(
        IIdentityAccountService identityAccountService,
        IOtpService otpService,
        IStringLocalizer<SharedResource> localizer)
    {
        _identityAccountService = identityAccountService;
        _otpService = otpService;
        _localizer = localizer;
    }

    public async Task Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Identifier))
        {
            return;
        }

        var otpResult = await _identityAccountService.GeneratePasswordResetOtpAsync(request.Identifier, cancellationToken);
        if (otpResult.Status == OtpDispatchStatus.Failed)
        {
            var errors = string.Join(", ", otpResult.Errors ?? []);
            throw new BusinessRuleException("IDENTITY_OPERATION_FAILED", $"{_localizer["IDENTITY_OPERATION_FAILED"]}: {errors}");
        }

        if (otpResult.Status != OtpDispatchStatus.Succeeded || otpResult.Account == null || string.IsNullOrWhiteSpace(otpResult.OtpCode))
        {
            return;
        }

        var user = otpResult.Account;
        if (!string.IsNullOrWhiteSpace(user.PhoneNumber))
        {
            await _otpService.SendOtpSmsAsync(user.PhoneNumber, otpResult.OtpCode, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            await _otpService.SendOtpEmailAsync(user.Email, otpResult.OtpCode, cancellationToken);
        }
    }
}
