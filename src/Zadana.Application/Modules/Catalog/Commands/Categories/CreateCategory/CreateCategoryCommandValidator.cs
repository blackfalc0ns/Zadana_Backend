using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Catalog.Commands.Categories.CreateCategory;

public class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(v => v.NameAr)
            .NotEmpty().WithMessage(localizer["RequiredField", "NameAr"])
            .MaximumLength(200).WithMessage(localizer["MaxLength", "NameAr", 200]);

        RuleFor(v => v.NameEn)
            .NotEmpty().WithMessage(localizer["RequiredField", "NameEn"])
            .MaximumLength(200).WithMessage(localizer["MaxLength", "NameEn", 200]);
    }
}
