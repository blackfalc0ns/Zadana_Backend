using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Identity.Commands.VerifyOtp;

public class VerifyOtpCommandValidator : AbstractValidator<VerifyOtpCommand>
{
    public VerifyOtpCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.Identifier)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .EmailAddress().WithMessage(x => localizer["InvalidEmail"])
            .WithName(x => localizer["Email"]);

        RuleFor(x => x.OtpCode)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .Length(4).WithMessage(x => localizer["InvalidOTP"])
            .WithName(x => "OTP Code");
    }
}
