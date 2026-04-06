using MediatR;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.SharedKernel.Exceptions;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Identity.Commands.ResetPassword;

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand>
{
    private readonly IIdentityAccountService _identityAccountService;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public ResetPasswordCommandHandler(
        IIdentityAccountService identityAccountService,
        IStringLocalizer<SharedResource> localizer)
    {
        _identityAccountService = identityAccountService;
        _localizer = localizer;
    }

    public async Task Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Identifier))
        {
            throw new UnauthorizedException(_localizer["InvalidResetAttempt"]);
        }

        var resetResult = await _identityAccountService.ResetPasswordAsync(
            request.Identifier,
            request.OtpCode,
            request.NewPassword,
            cancellationToken);

        if (resetResult.Status == PasswordResetStatus.UserNotFound)
        {
            throw new UnauthorizedException(_localizer["InvalidResetAttempt"]);
        }

        if (resetResult.Status == PasswordResetStatus.InvalidOrExpiredOtp)
        {
            throw new BusinessRuleException("INVALID_OTP", _localizer["InvalidOrExpiredOtp"]);
        }

        if (resetResult.Status == PasswordResetStatus.Failed)
        {
            var errors = string.Join(", ", resetResult.Errors ?? []);
            throw new BusinessRuleException("PASSWORD_RESET_FAILED", $"{_localizer["PASSWORD_RESET_FAILED"]}: {errors}");
        }
    }
}
