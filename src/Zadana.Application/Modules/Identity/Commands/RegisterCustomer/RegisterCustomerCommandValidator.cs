using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Identity.Commands.RegisterCustomer;

public class RegisterCustomerCommandValidator : AbstractValidator<RegisterCustomerCommand>
{
    public RegisterCustomerCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .MaximumLength(100).WithMessage(localizer["MaxLength"].Value)
            .WithName(localizer["FullName"].Value);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .EmailAddress().WithMessage(localizer["InvalidEmail"].Value)
            .MaximumLength(255).WithMessage(localizer["MaxLength"].Value)
            .WithName(localizer["Email"].Value);

        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .MaximumLength(20).WithMessage(localizer["MaxLength"].Value)
            .WithName(localizer["Phone"].Value);


        RuleFor(x => x.Password)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .MinimumLength(8).WithMessage(localizer["MinLength"].Value)
            .WithName(localizer["Password"].Value);


        RuleFor(x => x.AddressLine)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .MaximumLength(500).WithMessage(localizer["MaxLength"].Value)
            .WithName(localizer["AddressLine"].Value);

        RuleFor(x => x.Label)
            .IsEnumName(typeof(Zadana.Domain.Modules.Identity.Enums.AddressLabel), caseSensitive: false)
            .When(x => !string.IsNullOrEmpty(x.Label))
            .WithMessage(localizer["InvalidEnum"].Value)
            .WithName(localizer["Label"].Value);

        RuleFor(x => x.City).MaximumLength(100).WithMessage(localizer["MaxLength"].Value).WithName(localizer["City"].Value);
        RuleFor(x => x.Area).MaximumLength(100).WithMessage(localizer["MaxLength"].Value).WithName(localizer["Area"].Value);

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue).WithMessage(localizer["InvalidRange"].Value)
            .WithName(localizer["Latitude"].Value);

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue).WithMessage(localizer["InvalidRange"].Value)
            .WithName(localizer["Longitude"].Value);
    }
}
