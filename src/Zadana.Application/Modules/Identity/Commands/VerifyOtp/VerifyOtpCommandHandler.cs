using Microsoft.AspNetCore.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Identity.Commands.VerifyOtp;

public class VerifyOtpCommandHandler : IRequestHandler<VerifyOtpCommand, bool>
{
    private readonly UserManager<User> _userManager;

    public VerifyOtpCommandHandler(UserManager<User> userManager)
    {
        _userManager = userManager;
    }

    public async Task<bool> Handle(VerifyOtpCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Email == request.Identifier || u.PhoneNumber == request.Identifier, cancellationToken);
        
        if (user == null)
            throw new NotFoundException("User", request.Identifier);

        bool isValid = user.VerifyOtp(request.OtpCode);

        if (!isValid)
            throw new BusinessRuleException("INVALID_OTP", "الكود غير صحيح أو منتهي الصلاحية. | The OTP code is invalid or expired.");

        await _userManager.UpdateAsync(user);

        return true;
    }
}
