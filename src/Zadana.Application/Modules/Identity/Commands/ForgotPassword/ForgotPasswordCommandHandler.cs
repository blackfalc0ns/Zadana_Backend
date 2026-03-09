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
    private readonly IStringLocalizer<SharedResource> _localizer;

    public ForgotPasswordCommandHandler(
        UserManager<User> userManager,
        IOtpService otpService,
        IStringLocalizer<SharedResource> localizer)
    {
        _userManager = userManager;
        _otpService = otpService;
        _localizer = localizer;
    }

    public async Task Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Identifier))
        {
            return;
        }

        var user = await _userManager.FindByEmailAsync(request.Identifier);
        if (user == null)
        {
            user = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == request.Identifier, cancellationToken);
        }
        
        // Enhance security: We don't throw an error if the user isn't found to prevent email enumeration.
        if (user == null)
        {
            return; 
        }

        // Cooldown check
        if (!user.CanResendOtp())
        {
            return; // Or throw a specific exception if preferred, but usually silent for security in Forgot Password
        }

        // Generate the reset OTP
        var resetOtp = user.GeneratePasswordResetOtp();

        // Send OTP via SMS
        if (!string.IsNullOrWhiteSpace(user.PhoneNumber))
        {
            await _otpService.SendOtpSmsAsync(user.PhoneNumber, resetOtp, cancellationToken);
        }

        // Send OTP via Email (if email is provided)
        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            await _otpService.SendOtpEmailAsync(user.Email, resetOtp, cancellationToken);
        }

        await _userManager.UpdateAsync(user);
    }
}
