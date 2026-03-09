using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Identity.Commands.VerifyOtp;

public class VerifyOtpCommandHandler : IRequestHandler<VerifyOtpCommand, AuthResponseDto>
{
    private readonly UserManager<User> _userManager;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IUnitOfWork _unitOfWork;

    public VerifyOtpCommandHandler(
        UserManager<User> userManager,
        IJwtTokenService jwtTokenService,
        IUnitOfWork unitOfWork)
    {
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
        _unitOfWork = unitOfWork;
    }

    public async Task<AuthResponseDto> Handle(VerifyOtpCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Identifier))
        {
            throw new BusinessRuleException("EMAIL_REQUIRED", "البريد الإلكتروني مطلوب. | Email is required.");
        }

        var user = await _userManager.FindByEmailAsync(request.Identifier);

        if (user == null)
        {
            throw new BusinessRuleException("USER_NOT_FOUND", "المستخدم غير موجود. | User not found.");
        }

        if (!user.VerifyOtp(request.OtpCode))
        {
            throw new BusinessRuleException("INVALID_OTP", "كود التحقق غير صحيح أو منتهي الصلاحية. | Invalid or expired OTP.");
        }

        user.VerifyEmail(); // Mark email as confirmed after successful OTP verification
        var result = await _userManager.UpdateAsync(user);
        
        if (!result.Succeeded)
        {
             throw new BusinessRuleException("VERIFICATION_FAILED", "فشل تفعيل الحساب. | Failed to activate account.");
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var tokens = await _jwtTokenService.GenerateTokenPairAsync(user, cancellationToken);
        var userDto = new CurrentUserDto(user.Id, user.FullName, user.Email!, user.PhoneNumber!, user.Role.ToString());

        return new AuthResponseDto(tokens, userDto, true, "تم تفعيل الحساب بنجاح. | Account activated successfully.");
    }
}
