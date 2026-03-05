using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Catalog.Commands.Brands.UpdateBrand;

public class UpdateBrandCommandValidator : AbstractValidator<UpdateBrandCommand>
{
    public UpdateBrandCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage(localizer["RequiredField", "Id"]);

        RuleFor(x => x.NameAr)
            .NotEmpty().WithMessage(localizer["RequiredField", "NameAr"])
            .MaximumLength(150).WithMessage(localizer["MaxLength", 150]);

        RuleFor(x => x.NameEn)
            .NotEmpty().WithMessage(localizer["RequiredField", "NameEn"])
            .MaximumLength(150).WithMessage(localizer["MaxLength", 150]);

        RuleFor(x => x.LogoUrl)
            .MaximumLength(1000).WithMessage(localizer["MaxLength", 1000]);
    }
}
