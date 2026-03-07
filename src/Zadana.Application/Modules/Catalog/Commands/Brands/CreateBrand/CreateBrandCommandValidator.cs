using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Catalog.Commands.Brands.CreateBrand;

public class CreateBrandCommandValidator : AbstractValidator<CreateBrandCommand>
{
    public CreateBrandCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.NameAr)
            .NotEmpty().WithMessage(localizer["RequiredField", "NameAr"])
            .MaximumLength(150).WithMessage(localizer["MaxLength", "NameAr", 150]);

        RuleFor(x => x.NameEn)
            .NotEmpty().WithMessage(localizer["RequiredField", "NameEn"])
            .MaximumLength(150).WithMessage(localizer["MaxLength", "NameEn", 150]);

        RuleFor(x => x.LogoUrl)
            .MaximumLength(1000).WithMessage(localizer["MaxLength", "LogoUrl", 1000]);
    }
}
