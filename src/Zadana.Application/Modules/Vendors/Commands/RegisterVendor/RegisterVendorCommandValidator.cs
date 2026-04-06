using FluentValidation;

namespace Zadana.Application.Modules.Vendors.Commands.RegisterVendor;

public class RegisterVendorCommandValidator : AbstractValidator<RegisterVendorCommand>
{
    public RegisterVendorCommandValidator(Microsoft.Extensions.Localization.IStringLocalizer<Zadana.Application.Common.Localization.SharedResource> localizer)
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

        // Vendor
        RuleFor(x => x.BusinessNameAr)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .MaximumLength(200).WithMessage(localizer["MaxLength"].Value)
            .WithName(localizer["BusinessNameAr"].Value);
        RuleFor(x => x.BusinessNameEn)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .MaximumLength(200).WithMessage(localizer["MaxLength"].Value)
            .WithName(localizer["BusinessNameEn"].Value);
        RuleFor(x => x.BusinessType)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .MaximumLength(100).WithMessage(localizer["MaxLength"].Value)
            .WithName(localizer["BusinessType"].Value);
        RuleFor(x => x.CommercialRegistrationNumber)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .MaximumLength(50).WithMessage(localizer["MaxLength"].Value)
            .WithName(localizer["CommercialRegistrationNumber"].Value);
        RuleFor(x => x.ContactEmail)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .EmailAddress().WithMessage(localizer["InvalidEmail"].Value)
            .MaximumLength(255).WithMessage(localizer["MaxLength"].Value)
            .WithName(localizer["ContactEmail"].Value);
        RuleFor(x => x.ContactPhone)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .MaximumLength(20).WithMessage(localizer["MaxLength"].Value)
            .WithName(localizer["ContactPhone"].Value);
        RuleFor(x => x.OwnerName)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .MaximumLength(200).WithMessage(localizer["MaxLength"].Value);
        RuleFor(x => x.OwnerEmail)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .EmailAddress().WithMessage(localizer["InvalidEmail"].Value)
            .MaximumLength(255).WithMessage(localizer["MaxLength"].Value);
        RuleFor(x => x.OwnerPhone)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .MaximumLength(20).WithMessage(localizer["MaxLength"].Value);
        RuleFor(x => x.Region)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .MaximumLength(100).WithMessage(localizer["MaxLength"].Value);
        RuleFor(x => x.City)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .MaximumLength(100).WithMessage(localizer["MaxLength"].Value);
        RuleFor(x => x.NationalAddress)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .MaximumLength(500).WithMessage(localizer["MaxLength"].Value);
        RuleFor(x => x.LicenseNumber)
            .MaximumLength(100).WithMessage(localizer["MaxLength"].Value);
        RuleFor(x => x.BankName)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .MaximumLength(200).WithMessage(localizer["MaxLength"].Value);
        RuleFor(x => x.AccountHolderName)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .MaximumLength(200).WithMessage(localizer["MaxLength"].Value);
        RuleFor(x => x.Iban)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .MaximumLength(34).WithMessage(localizer["MaxLength"].Value);
        RuleFor(x => x.SwiftCode)
            .MaximumLength(11).WithMessage(localizer["MaxLength"].Value);
        RuleFor(x => x.PayoutCycle)
            .MaximumLength(50).WithMessage(localizer["MaxLength"].Value);

        // Branch
        RuleFor(x => x.BranchName)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .MaximumLength(100).WithMessage(localizer["MaxLength"].Value)
            .WithName(localizer["BranchName"].Value);
        RuleFor(x => x.BranchAddressLine)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .MaximumLength(300).WithMessage(localizer["MaxLength"].Value)
            .WithName(localizer["BranchAddressLine"].Value);
        RuleFor(x => x.BranchLatitude)
            .InclusiveBetween(-90, 90).WithMessage(localizer["InvalidRange"].Value)
            .WithName(localizer["BranchLatitude"].Value);
        RuleFor(x => x.BranchLongitude)
            .InclusiveBetween(-180, 180).WithMessage(localizer["InvalidRange"].Value)
            .WithName(localizer["BranchLongitude"].Value);
        RuleFor(x => x.BranchContactPhone)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .MaximumLength(20).WithMessage(localizer["MaxLength"].Value)
            .WithName(localizer["BranchContactPhone"].Value);
        RuleFor(x => x.BranchDeliveryRadiusKm)
            .GreaterThan(0).WithMessage(localizer["GreaterThanZero"].Value)
            .WithName(localizer["BranchDeliveryRadiusKm"].Value);
    }
}
