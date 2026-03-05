using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Vendors.Commands.UpdateVendorProfile;

public class UpdateVendorProfileCommandValidator : AbstractValidator<UpdateVendorProfileCommand>
{
    public UpdateVendorProfileCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
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

        RuleFor(x => x.ContactEmail)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .EmailAddress().WithMessage(x => localizer["InvalidEmail"])
            .MaximumLength(256).WithMessage(x => localizer["MaxLength"])
            .WithName(x => localizer["ContactEmail"]);

        RuleFor(x => x.ContactPhone)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(20).WithMessage(x => localizer["MaxLength"])
            .WithName(x => localizer["ContactPhone"]);

        RuleFor(x => x.TaxId)
            .MaximumLength(50).WithMessage(x => localizer["MaxLength"])
            .When(x => x.TaxId != null)
            .WithName(x => localizer["TaxId"]);
    }
}
