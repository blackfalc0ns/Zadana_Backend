using MediatR;
using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Identity.Commands.ResetPassword;

public record ResetPasswordCommand(string Identifier, string OtpCode, string NewPassword) : IRequest;

public class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.Identifier)
            .NotEmpty().WithMessage(localizer["RequiredField", localizer["Identifier"].Value]);

        RuleFor(x => x.OtpCode)
            .NotEmpty().WithMessage(localizer["RequiredField", localizer["OtpCode"].Value])
            .Length(4).WithMessage(localizer["OtpCodeMustBe4Digits"]);

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage(localizer["RequiredField", localizer["NewPassword"].Value])
            .MinimumLength(8).WithMessage(localizer["PasswordMinLength"]);
    }
}
