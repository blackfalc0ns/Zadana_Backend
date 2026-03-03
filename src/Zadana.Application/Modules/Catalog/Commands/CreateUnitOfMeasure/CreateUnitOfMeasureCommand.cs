using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Catalog.Commands.CreateUnitOfMeasure;

public record CreateUnitOfMeasureCommand(
    string Name,
    string Abbreviation) : MediatR.IRequest<Guid>;

public class CreateUnitOfMeasureCommandValidator : AbstractValidator<CreateUnitOfMeasureCommand>
{
    public CreateUnitOfMeasureCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(50).WithMessage(x => localizer["MaxLength"]);

        RuleFor(x => x.Abbreviation)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(20).WithMessage(x => localizer["MaxLength"]);
    }
}
