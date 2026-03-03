using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Identity.Commands.RegisterUser;

public record RegisterUserCommand(
    string FullName,
    string Email,
    string Phone,
    string Password,
    string Role) : MediatR.IRequest<Guid>;

public class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(200).WithMessage(x => localizer["MaxLength"]);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .EmailAddress().WithMessage(x => localizer["InvalidEmail"])
            .MaximumLength(200).WithMessage(x => localizer["MaxLength"]);

        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(50).WithMessage(x => localizer["MaxLength"]);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MinimumLength(8).WithMessage("Password must be at least 8 characters long.");

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("Role is required.")
            .IsEnumName(typeof(Domain.Modules.Identity.Enums.UserRole), caseSensitive: false)
            .WithMessage("Invalid User Role. Must be Admin, Customer, Vendor, or Driver.");
    }
}
