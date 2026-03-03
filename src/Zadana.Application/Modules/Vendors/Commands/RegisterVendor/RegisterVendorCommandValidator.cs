using FluentValidation;

namespace Zadana.Application.Modules.Vendors.Commands.RegisterVendor;

public class RegisterVendorCommandValidator : AbstractValidator<RegisterVendorCommand>
{
    public RegisterVendorCommandValidator()
    {
        // User
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);

        // Vendor
        RuleFor(x => x.BusinessNameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BusinessNameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BusinessType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CommercialRegistrationNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ContactEmail).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.ContactPhone).NotEmpty().MaximumLength(20);

        // Branch
        RuleFor(x => x.BranchName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.BranchAddressLine).NotEmpty().MaximumLength(300);
        RuleFor(x => x.BranchLatitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.BranchLongitude).InclusiveBetween(-180, 180);
        RuleFor(x => x.BranchContactPhone).NotEmpty().MaximumLength(20);
        RuleFor(x => x.BranchDeliveryRadiusKm).GreaterThan(0);
    }
}
