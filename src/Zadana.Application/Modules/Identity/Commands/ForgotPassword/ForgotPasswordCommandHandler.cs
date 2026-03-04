using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Identity.Interfaces;
using Zadana.SharedKernel.Exceptions;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Identity.Commands.ForgotPassword;

public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOtpService _otpService;
    private readonly IEmailService _emailService;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public ForgotPasswordCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IOtpService otpService,
        IEmailService emailService,
        IStringLocalizer<SharedResource> localizer)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _otpService = otpService;
        _emailService = emailService;
        _localizer = localizer;
    }

    public async Task Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdentifierAsync(request.Identifier, cancellationToken);
        
        // Enhance security: We don't throw an error if the user isn't found to prevent email enumeration.
        if (user == null)
        {
            return; 
        }

        // Generate the reset OTP
        var resetOtp = user.GeneratePasswordResetOtp();

        // Send OTP via SMS
        await _otpService.SendOtpSmsAsync(user.Phone, resetOtp, cancellationToken);

        // Send OTP via Email (if email is provided)
        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            await _emailService.SendEmailAsync(
                user.Email,
                _localizer["PasswordResetEmailSubject"],
                string.Format(_localizer["PasswordResetEmailBody"].Value, resetOtp),
                cancellationToken);
        }

        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
