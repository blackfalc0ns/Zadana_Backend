using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Identity.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Identity.Commands.VerifyOtp;

public class VerifyOtpCommandHandler : IRequestHandler<VerifyOtpCommand, bool>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public VerifyOtpCommandHandler(IUserRepository userRepository, IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(VerifyOtpCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdentifierAsync(request.Identifier, cancellationToken);
        
        if (user == null)
            throw new NotFoundException("User", request.Identifier);

        bool isValid = user.VerifyOtp(request.OtpCode);

        if (!isValid)
            throw new BusinessRuleException("INVALID_OTP", "الكود غير صحيح أو منتهي الصلاحية. | The OTP code is invalid or expired.");

        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
