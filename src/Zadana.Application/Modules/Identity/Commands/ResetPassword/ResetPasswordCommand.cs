using MediatR;
using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Identity.Commands.ResetPassword;

public record ResetPasswordCommand(string Identifier, string ResetToken, string NewPassword) : IRequest;

public class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.Identifier)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .WithName(localizer["Identifier"].Value);

        RuleFor(x => x.ResetToken)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .MinimumLength(32).WithMessage(localizer["InvalidResetToken"].Value)
            .WithName(localizer["ResetToken"].Value);

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .MinimumLength(8).WithMessage(localizer["PasswordMinLength"].Value)
            .WithName(localizer["NewPassword"].Value);
    }
}
