using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Delivery.Commands.RegisterDriver;

public class RegisterDriverCommandValidator : AbstractValidator<RegisterDriverCommand>
{
    public RegisterDriverCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        // User
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
            .MinimumLength(8).WithMessage(x => localizer["MinLength8"])
            .WithName(x => localizer["Password"]);

        // Driver Details
        RuleFor(x => x.VehicleType).MaximumLength(50).WithMessage(x => localizer["MaxLength"]).WithName(x => localizer["VehicleType"]);
        RuleFor(x => x.NationalId)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(20).WithMessage(x => localizer["MaxLength"])
            .WithName(x => localizer["NationalId"]);
        RuleFor(x => x.LicenseNumber)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(30).WithMessage(x => localizer["MaxLength"])
            .WithName(x => localizer["LicenseNumber"]);
        RuleFor(x => x.Address).MaximumLength(300).WithMessage(x => localizer["MaxLength"]).WithName(x => localizer["Address"]);

        // Document URLs
        RuleFor(x => x.NationalIdImageUrl)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .WithName(x => localizer["NationalIdImageUrl"]);
        RuleFor(x => x.LicenseImageUrl)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .WithName(x => localizer["LicenseImageUrl"]);
        RuleFor(x => x.VehicleImageUrl)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .WithName(x => localizer["VehicleImageUrl"]);
        RuleFor(x => x.PersonalPhotoUrl)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .WithName(x => localizer["PersonalPhotoUrl"]);
    }
}
