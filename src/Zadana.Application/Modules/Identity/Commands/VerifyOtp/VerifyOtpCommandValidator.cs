using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Identity.Commands.VerifyOtp;

public class VerifyOtpCommandValidator : AbstractValidator<VerifyOtpCommand>
{
    public VerifyOtpCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.Identifier)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .EmailAddress().WithMessage(localizer["InvalidEmail"].Value)
            .WithName(localizer["Email"].Value);

        RuleFor(x => x.OtpCode)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .Length(4).WithMessage(localizer["InvalidOtpLength"].Value)
            .WithName(localizer["OtpCode"].Value);
    }
}
