using MediatR;
using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Identity.Commands.ForgotPassword;

public record ForgotPasswordCommand(string Identifier) : IRequest;

public class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.Identifier)
            .NotEmpty().WithMessage(localizer["RequiredField", localizer["Identifier"].Value]);
    }
}
