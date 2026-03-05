using FluentValidation;

namespace Zadana.Application.Modules.Vendors.Commands.RegisterVendor;

public class RegisterVendorCommandValidator : AbstractValidator<RegisterVendorCommand>
{
    public RegisterVendorCommandValidator(Microsoft.Extensions.Localization.IStringLocalizer<Zadana.Application.Common.Localization.SharedResource> localizer)
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
            .MinimumLength(8).WithMessage(x => localizer["PasswordMinLength"])
            .WithName(x => localizer["Password"]);

        // Vendor
        RuleFor(x => x.BusinessNameAr)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(200).WithMessage(x => localizer["MaxLength"])
            .WithName(x => localizer["BusinessNameAr"]);
        RuleFor(x => x.BusinessNameEn)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(200).WithMessage(x => localizer["MaxLength"])
            .WithName(x => localizer["BusinessNameEn"]);
        RuleFor(x => x.BusinessType)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(100).WithMessage(x => localizer["MaxLength"])
            .WithName(x => localizer["BusinessType"]);
        RuleFor(x => x.CommercialRegistrationNumber)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(50).WithMessage(x => localizer["MaxLength"])
            .WithName(x => localizer["CommercialRegistrationNumber"]);
        RuleFor(x => x.ContactEmail)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .EmailAddress().WithMessage(x => localizer["InvalidEmail"])
            .MaximumLength(255).WithMessage(x => localizer["MaxLength"])
            .WithName(x => localizer["ContactEmail"]);
        RuleFor(x => x.ContactPhone)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(20).WithMessage(x => localizer["MaxLength"])
            .WithName(x => localizer["ContactPhone"]);

        // Branch
        RuleFor(x => x.BranchName)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(100).WithMessage(x => localizer["MaxLength"])
            .WithName(x => localizer["BranchName"]);
        RuleFor(x => x.BranchAddressLine)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(300).WithMessage(x => localizer["MaxLength"])
            .WithName(x => localizer["BranchAddressLine"]);
        RuleFor(x => x.BranchLatitude)
            .InclusiveBetween(-90, 90).WithMessage(x => localizer["InvalidRange"])
            .WithName(x => localizer["BranchLatitude"]);
        RuleFor(x => x.BranchLongitude)
            .InclusiveBetween(-180, 180).WithMessage(x => localizer["InvalidRange"])
            .WithName(x => localizer["BranchLongitude"]);
        RuleFor(x => x.BranchContactPhone)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(20).WithMessage(x => localizer["MaxLength"])
            .WithName(x => localizer["BranchContactPhone"]);
        RuleFor(x => x.BranchDeliveryRadiusKm)
            .GreaterThan(0).WithMessage(x => localizer["GreaterThanZero"])
            .WithName(x => localizer["BranchDeliveryRadiusKm"]);
    }
}
