using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Catalog.Commands.Categories.UpdateCategory;

public class UpdateCategoryCommandValidator : AbstractValidator<UpdateCategoryCommand>
{
    public UpdateCategoryCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(v => v.Id)
            .NotEmpty().WithMessage(localizer["RequiredField", "Id"]);

        RuleFor(v => v.NameAr)
            .NotEmpty().WithMessage(localizer["RequiredField", "NameAr"])
            .MaximumLength(200).WithMessage(localizer["MaxLength", 200]);

        RuleFor(v => v.NameEn)
            .NotEmpty().WithMessage(localizer["RequiredField", "NameEn"])
            .MaximumLength(200).WithMessage(localizer["MaxLength", 200]);
    }
}
