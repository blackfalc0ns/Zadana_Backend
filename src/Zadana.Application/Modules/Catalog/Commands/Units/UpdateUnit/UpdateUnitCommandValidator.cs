using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Catalog.Commands.Units.UpdateUnit;

public class UpdateUnitCommandValidator : AbstractValidator<UpdateUnitCommand>
{
    public UpdateUnitCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage(localizer["RequiredField", "Id"]);

        RuleFor(x => x.NameAr)
            .NotEmpty().WithMessage(localizer["RequiredField", "NameAr"])
            .MaximumLength(100).WithMessage(localizer["MaxLength", "NameAr", 100]);

        RuleFor(x => x.NameEn)
            .NotEmpty().WithMessage(localizer["RequiredField", "NameEn"])
            .MaximumLength(100).WithMessage(localizer["MaxLength", "NameEn", 100]);

        RuleFor(x => x.Symbol)
            .MaximumLength(20).WithMessage(localizer["MaxLength", "Symbol", 20]);
    }
}
