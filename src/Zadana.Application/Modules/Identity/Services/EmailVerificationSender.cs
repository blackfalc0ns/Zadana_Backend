using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Identity.Services;

public interface IEmailVerificationSender
{
    Task SendAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed class EmailVerificationSender : IEmailVerificationSender
{
    private readonly IIdentityAccountService _identityAccountService;
    private readonly IOtpService _otpService;

    public EmailVerificationSender(
        IIdentityAccountService identityAccountService,
        IOtpService otpService)
    {
        _identityAccountService = identityAccountService;
        _otpService = otpService;
    }

    public async Task SendAsync(Guid userId, CancellationToken cancellationToken)
    {
        var otpResult = await _identityAccountService.GenerateRegistrationOtpAsync(userId, cancellationToken);
        if (otpResult.Status == OtpDispatchStatus.UserNotFound)
        {
            throw new NotFoundException("User", userId);
        }

        if (otpResult.Status != OtpDispatchStatus.Succeeded ||
            otpResult.Account is null ||
            string.IsNullOrWhiteSpace(otpResult.Account.Email) ||
            string.IsNullOrWhiteSpace(otpResult.OtpCode))
        {
            throw new BusinessRuleException(
                "EMAIL_VERIFICATION_OTP_FAILED",
                string.Join(", ", otpResult.Errors ?? ["Unable to create an email verification code."]));
        }

        await _otpService.SendOtpEmailAsync(
            otpResult.Account.Email,
            otpResult.OtpCode,
            cancellationToken);
    }
}
