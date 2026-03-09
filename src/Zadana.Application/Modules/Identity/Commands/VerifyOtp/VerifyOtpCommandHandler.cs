using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.SharedKernel.Exceptions;
using Zadana.Application.Common.Localization;
using Microsoft.Extensions.Localization;

namespace Zadana.Application.Modules.Identity.Commands.VerifyOtp;

public class VerifyOtpCommandHandler : IRequestHandler<VerifyOtpCommand, AuthResponseDto>
{
    private readonly UserManager<User> _userManager;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public VerifyOtpCommandHandler(
        UserManager<User> userManager,
        IJwtTokenService jwtTokenService,
        IUnitOfWork unitOfWork,
        IStringLocalizer<SharedResource> localizer)
    {
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
        _unitOfWork = unitOfWork;
        _localizer = localizer;
    }

    public async Task<AuthResponseDto> Handle(VerifyOtpCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Identifier))
        {
            throw new BusinessRuleException("EMAIL_REQUIRED", _localizer["RequiredField", _localizer["Email"].Value]);
        }

        var user = await _userManager.FindByEmailAsync(request.Identifier);

        if (user == null)
        {
            throw new BusinessRuleException("USER_NOT_FOUND", _localizer["USER_NOT_FOUND", request.Identifier]);
        }

        if (!user.VerifyOtp(request.OtpCode))
        {
            throw new BusinessRuleException("INVALID_OTP", _localizer["InvalidOrExpiredOtp"]);
        }

        user.VerifyEmail(); // Mark email as confirmed after successful OTP verification
        var result = await _userManager.UpdateAsync(user);
        
        if (!result.Succeeded)
        {
             throw new BusinessRuleException("VERIFICATION_FAILED", _localizer["VERIFICATION_FAILED"]);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var tokens = await _jwtTokenService.GenerateTokenPairAsync(user, cancellationToken);
        var userDto = new CurrentUserDto(user.Id, user.FullName, user.Email!, user.PhoneNumber!, user.Role.ToString());

        return new AuthResponseDto(tokens, userDto, true, _localizer["AccountVerifiedSuccessfully"]);
    }
}
