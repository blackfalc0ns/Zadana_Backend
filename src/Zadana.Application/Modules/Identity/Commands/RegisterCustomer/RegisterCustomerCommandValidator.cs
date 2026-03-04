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
            .MinimumLength(8).WithMessage(x => localizer["MinLength"])
            .WithName(x => localizer["Password"]);


        RuleFor(x => x.AddressLine)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(500).WithMessage(x => localizer["MaxLength"])
            .WithName(x => localizer["AddressLine"]);

        RuleFor(x => x.Label)
            .IsEnumName(typeof(Zadana.Domain.Modules.Identity.Enums.AddressLabel), caseSensitive: false)
            .When(x => !string.IsNullOrEmpty(x.Label))
            .WithMessage(x => localizer["InvalidEnum"])
            .WithName(x => localizer["Label"]);

        RuleFor(x => x.City).MaximumLength(100).WithMessage(x => localizer["MaxLength"]).WithName(x => localizer["City"]);
        RuleFor(x => x.Area).MaximumLength(100).WithMessage(x => localizer["MaxLength"]).WithName(x => localizer["Area"]);

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue).WithMessage(x => localizer["InvalidRange"])
            .WithName(x => localizer["Latitude"]);

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue).WithMessage(x => localizer["InvalidRange"])
            .WithName(x => localizer["Longitude"]);
    }
}
