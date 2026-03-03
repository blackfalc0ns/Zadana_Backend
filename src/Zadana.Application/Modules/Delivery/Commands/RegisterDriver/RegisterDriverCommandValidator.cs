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
            .MaximumLength(100).WithMessage(x => localizer["MaxLength"]);
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .EmailAddress().WithMessage(x => localizer["InvalidEmail"])
            .MaximumLength(255).WithMessage(x => localizer["MaxLength"]);
        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(20).WithMessage(x => localizer["MaxLength"]);
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MinimumLength(8).WithMessage("كلمة المرور يجب أن تكون 8 أحرف على الأقل. | Password must be at least 8 characters.");

        // Driver Details
        RuleFor(x => x.VehicleType).MaximumLength(50).WithMessage(x => localizer["MaxLength"]);
        RuleFor(x => x.NationalId)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(20).WithMessage(x => localizer["MaxLength"]);
        RuleFor(x => x.LicenseNumber)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(30).WithMessage(x => localizer["MaxLength"]);
        RuleFor(x => x.Address).MaximumLength(300).WithMessage(x => localizer["MaxLength"]);

        // Document URLs
        RuleFor(x => x.NationalIdImageUrl)
            .NotEmpty().WithMessage(x => localizer["RequiredField"]);
        RuleFor(x => x.LicenseImageUrl)
            .NotEmpty().WithMessage(x => localizer["RequiredField"]);
        RuleFor(x => x.VehicleImageUrl)
            .NotEmpty().WithMessage(x => localizer["RequiredField"]);
        RuleFor(x => x.PersonalPhotoUrl)
            .NotEmpty().WithMessage(x => localizer["RequiredField"]);
    }
}
