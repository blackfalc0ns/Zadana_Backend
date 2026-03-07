using Microsoft.AspNetCore.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Interfaces;
using Zadana.SharedKernel.Exceptions;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Identity.Commands.ForgotPassword;

public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand>
{
    private readonly UserManager<User> _userManager;
    private readonly IOtpService _otpService;
    private readonly IEmailService _emailService;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public ForgotPasswordCommandHandler(
        UserManager<User> userManager,
        IOtpService otpService,
        IEmailService emailService,
        IStringLocalizer<SharedResource> localizer)
    {
        _userManager = userManager;
        _otpService = otpService;
        _emailService = emailService;
        _localizer = localizer;
    }

    public async Task Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Email == request.Identifier || u.PhoneNumber == request.Identifier, cancellationToken);
        
        // Enhance security: We don't throw an error if the user isn't found to prevent email enumeration.
        if (user == null)
        {
            return; 
        }

        // Generate the reset OTP
        var resetOtp = user.GeneratePasswordResetOtp();

        // Send OTP via SMS
        await _otpService.SendOtpSmsAsync(user.PhoneNumber, resetOtp, cancellationToken);

        // Send OTP via Email (if email is provided)
        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            await _emailService.SendEmailAsync(
                user.Email,
                _localizer["PasswordResetEmailSubject"],
                string.Format(_localizer["PasswordResetEmailBody"].Value, resetOtp),
                cancellationToken);
        }

        await _userManager.UpdateAsync(user);
    }
}
