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
            .MinimumLength(8).WithMessage(localizer["PasswordMinLength"].Value)
            .WithName(localizer["Password"].Value);

        // Driver Details
        RuleFor(x => x.VehicleType).MaximumLength(50).WithMessage(localizer["MaxLength"].Value).WithName(localizer["VehicleType"].Value);
        RuleFor(x => x.NationalId)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .MaximumLength(20).WithMessage(localizer["MaxLength"].Value)
            .WithName(localizer["NationalId"].Value);
        RuleFor(x => x.LicenseNumber)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .MaximumLength(30).WithMessage(localizer["MaxLength"].Value)
            .WithName(localizer["LicenseNumber"].Value);
        RuleFor(x => x.Address).MaximumLength(300).WithMessage(localizer["MaxLength"].Value).WithName(localizer["Address"].Value);

        // Document URLs
        RuleFor(x => x.NationalIdImageUrl)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .WithName(localizer["NationalIdImageUrl"].Value);
        RuleFor(x => x.LicenseImageUrl)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .WithName(localizer["LicenseImageUrl"].Value);
        RuleFor(x => x.VehicleImageUrl)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .WithName(localizer["VehicleImageUrl"].Value);
        RuleFor(x => x.PersonalPhotoUrl)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .WithName(localizer["PersonalPhotoUrl"].Value);
    }
}
