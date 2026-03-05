using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Catalog.Commands.Units.CreateUnit;

public class CreateUnitCommandValidator : AbstractValidator<CreateUnitCommand>
{
    public CreateUnitCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.NameAr)
            .NotEmpty().WithMessage(localizer["RequiredField", "NameAr"])
            .MaximumLength(100).WithMessage(localizer["MaxLength", 100]);

        RuleFor(x => x.NameEn)
            .NotEmpty().WithMessage(localizer["RequiredField", "NameEn"])
            .MaximumLength(100).WithMessage(localizer["MaxLength", 100]);

        RuleFor(x => x.Symbol)
            .MaximumLength(20).WithMessage(localizer["MaxLength", 20]);
    }
}
