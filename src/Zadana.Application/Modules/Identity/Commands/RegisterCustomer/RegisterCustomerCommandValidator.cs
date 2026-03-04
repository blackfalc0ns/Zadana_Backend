using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Identity.Commands.RegisterCustomer;

public class RegisterCustomerCommandValidator : AbstractValidator<RegisterCustomerCommand>
{
    public RegisterCustomerCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(100).WithMessage(x => localizer["MaxLength"])
            .WithName(x => localizer["FullName"]);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .EmailAddress().WithMessage(x => localizer["InvalidEmail"])
            .MaximumLength(255).WithMessage(x => localizer["MaxLength"])
            .WithName(x => localizer["Email"]);

        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(20).WithMessage(x => localizer["MaxLength"])
            .WithName(x => localizer["Phone"]);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MinimumLength(8).WithMessage("كلمة المرور يجب أن تكون 8 أحرف على الأقل. | Password must be at least 8 characters.")
            .WithName(x => localizer["Password"]);
    }
}
