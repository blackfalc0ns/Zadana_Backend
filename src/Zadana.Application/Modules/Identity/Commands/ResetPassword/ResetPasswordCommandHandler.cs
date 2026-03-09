using Microsoft.AspNetCore.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.SharedKernel.Exceptions;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Identity.Commands.ResetPassword;

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand>
{
    private readonly UserManager<User> _userManager;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public ResetPasswordCommandHandler(
        UserManager<User> userManager,
        IStringLocalizer<SharedResource> localizer)
    {
        _userManager = userManager;
        _localizer = localizer;
    }

    public async Task Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Identifier))
        {
            throw new UnauthorizedException(_localizer["InvalidResetAttempt"]);
        }

        var user = await _userManager.FindByEmailAsync(request.Identifier);
        if (user == null)
        {
            user = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == request.Identifier, cancellationToken);
        }
        
        if (user == null)
        {
            throw new UnauthorizedException(_localizer["InvalidResetAttempt"]);
        }

        var isValidOtp = user.VerifyPasswordResetOtp(request.OtpCode);
        if (!isValidOtp)
        {
            throw new BusinessRuleException("INVALID_OTP", _localizer["InvalidOrExpiredOtp"]);
        }

        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, resetToken, request.NewPassword);

        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new BusinessRuleException("PASSWORD_RESET_FAILED", $"{_localizer["PASSWORD_RESET_FAILED"]}: {errors}");
        }

        // UserManager automatically calls SaveChanges
    }
}
