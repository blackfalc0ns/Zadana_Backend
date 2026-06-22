using MediatR;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Identity.Commands.VerifyPasswordResetOtp;

public class VerifyPasswordResetOtpCommandHandler : IRequestHandler<VerifyPasswordResetOtpCommand, PasswordResetOtpVerifiedDto>
{
    private readonly IIdentityAccountService _identityAccountService;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public VerifyPasswordResetOtpCommandHandler(
        IIdentityAccountService identityAccountService,
        IStringLocalizer<SharedResource> localizer)
    {
        _identityAccountService = identityAccountService;
        _localizer = localizer;
    }

    public async Task<PasswordResetOtpVerifiedDto> Handle(VerifyPasswordResetOtpCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Identifier))
        {
            throw new UnauthorizedException(_localizer["InvalidResetAttempt"]);
        }

        var verificationResult = await _identityAccountService.VerifyPasswordResetOtpAsync(
            request.Identifier,
            request.OtpCode,
            cancellationToken);

        if (verificationResult.Status == PasswordResetOtpVerificationStatus.InvalidOrExpiredOtp)
        {
            throw new BusinessRuleException("INVALID_OTP", _localizer["InvalidOrExpiredOtp"]);
        }

        if (verificationResult.Status == PasswordResetOtpVerificationStatus.Failed)
        {
            var errors = string.Join(", ", verificationResult.Errors ?? []);
            var message = string.IsNullOrWhiteSpace(errors)
                ? _localizer["VERIFICATION_FAILED"].Value
                : errors;
            throw new BusinessRuleException("VERIFICATION_FAILED", message);
        }

        if (verificationResult.Status != PasswordResetOtpVerificationStatus.Succeeded ||
            string.IsNullOrWhiteSpace(verificationResult.ResetToken) ||
            verificationResult.ExpiresInSeconds is null)
        {
            throw new BusinessRuleException("INVALID_OTP", _localizer["InvalidOrExpiredOtp"]);
        }

        return new PasswordResetOtpVerifiedDto(
            verificationResult.ResetToken,
            verificationResult.ExpiresInSeconds.Value,
            _localizer["PasswordResetOtpVerified"].Value);
    }
}
