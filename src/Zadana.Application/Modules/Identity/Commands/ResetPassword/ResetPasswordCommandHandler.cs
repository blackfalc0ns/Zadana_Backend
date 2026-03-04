using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Identity.Interfaces;
using Zadana.SharedKernel.Exceptions;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Identity.Commands.ResetPassword;

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public ResetPasswordCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IStringLocalizer<SharedResource> localizer)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _localizer = localizer;
    }

    public async Task Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdentifierAsync(request.Identifier, cancellationToken);
        
        if (user == null)
        {
            throw new UnauthorizedException(_localizer["InvalidResetAttempt"]);
        }

        var isValidOtp = user.VerifyPasswordResetOtp(request.OtpCode);
        if (!isValidOtp)
        {
            throw new BusinessRuleException("INVALID_OTP", _localizer["InvalidOrExpiredOtp"]);
        }

        var newPasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.ChangePassword(newPasswordHash);

        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
