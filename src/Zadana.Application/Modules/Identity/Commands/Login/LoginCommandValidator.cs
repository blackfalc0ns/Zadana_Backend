using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Identity.Commands.Login;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.Identifier)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .WithName(localizer["PhoneOrEmail"].Value);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .WithName(localizer["Password"].Value);
    }
}
