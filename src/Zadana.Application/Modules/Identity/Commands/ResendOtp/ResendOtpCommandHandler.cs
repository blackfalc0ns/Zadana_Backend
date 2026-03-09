using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Identity.Commands.ResendOtp;

public class ResendOtpCommandHandler : IRequestHandler<ResendOtpCommand, AuthResponseDto>
{
    private readonly UserManager<User> _userManager;
    private readonly IOtpService _otpService;
    private readonly IUnitOfWork _unitOfWork;

    public ResendOtpCommandHandler(
        UserManager<User> userManager,
        IOtpService otpService,
        IUnitOfWork unitOfWork)
    {
        _userManager = userManager;
        _otpService = otpService;
        _unitOfWork = unitOfWork;
    }

    public async Task<AuthResponseDto> Handle(ResendOtpCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Identifier))
        {
            throw new BusinessRuleException("EMAIL_REQUIRED", "البريد الإلكتروني مطلوب. | Email is required.");
        }

        var user = await _userManager.FindByEmailAsync(request.Identifier);
        if (user == null)
        {
            user = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == request.Identifier, cancellationToken);
        }

        if (user == null)
        {
            throw new BusinessRuleException("USER_NOT_FOUND", "المستخدم غير موجود. | User not found.");
        }

        if (!user.CanResendOtp())
        {
            var timeLeft = 60 - (int)(DateTime.UtcNow - user.LastOtpSentAt!.Value).TotalSeconds;
            throw new BusinessRuleException("OTP_COOLDOWN", $"يرجى الانتظار {timeLeft} ثانية قبل إعادة المحاولة. | Please wait {timeLeft} seconds before retrying.");
        }

        var otpCode = user.GenerateOtp();
        await _userManager.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _otpService.SendOtpEmailAsync(user.Email!, otpCode, cancellationToken);

        var userDto = new CurrentUserDto(user.Id, user.FullName, user.Email!, user.PhoneNumber!, user.Role.ToString());
        return new AuthResponseDto(null, userDto, false, "تم إعادة إرسال كود التحقق بنجاح. | Verification code resent successfully.");
    }
}
