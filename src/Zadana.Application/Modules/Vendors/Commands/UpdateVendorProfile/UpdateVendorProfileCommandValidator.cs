using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Vendors.Commands.UpdateVendorProfile;

public class UpdateVendorProfileCommandValidator : AbstractValidator<UpdateVendorProfileCommand>
{
    public UpdateVendorProfileCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
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

        RuleFor(x => x.ContactEmail)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .EmailAddress().WithMessage(localizer["InvalidEmail"].Value)
            .MaximumLength(256).WithMessage(localizer["MaxLength"].Value)
            .WithName(localizer["ContactEmail"].Value);

        RuleFor(x => x.ContactPhone)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .MaximumLength(20).WithMessage(localizer["MaxLength"].Value)
            .WithName(localizer["ContactPhone"].Value);

        RuleFor(x => x.TaxId)
            .MaximumLength(50).WithMessage(localizer["MaxLength"].Value)
            .When(x => x.TaxId != null)
            .WithName("TaxId");
    }
}
