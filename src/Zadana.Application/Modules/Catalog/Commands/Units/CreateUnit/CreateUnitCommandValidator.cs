using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Catalog.Commands.Units.CreateUnit;

public class CreateUnitCommandValidator : AbstractValidator<CreateUnitCommand>
{
    public CreateUnitCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.NameAr)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .MaximumLength(100).WithMessage(localizer["MaxLength"].Value)
            .WithName("NameAr");

        RuleFor(x => x.NameEn)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .MaximumLength(100).WithMessage(localizer["MaxLength"].Value)
            .WithName("NameEn");

        RuleFor(x => x.Symbol)
            .MaximumLength(20).WithMessage(localizer["MaxLength"].Value)
            .WithName("Symbol");
    }
}
