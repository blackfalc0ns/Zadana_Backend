using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Catalog.Commands.CreateMasterProduct;

public record CreateMasterProductCommand(
    Guid CategoryId,
    string Name,
    string Slug,
    string? Barcode,
    string? Description,
    Guid? BrandId,
    Guid? UnitId) : MediatR.IRequest<Guid>;

public class CreateMasterProductCommandValidator : AbstractValidator<CreateMasterProductCommand>
{
    public CreateMasterProductCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.CategoryId).NotEmpty().WithMessage(x => localizer["RequiredField"]);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(250).WithMessage(x => localizer["MaxLength"]);

        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(250).WithMessage(x => localizer["MaxLength"]);

        RuleFor(x => x.Barcode)
            .MaximumLength(100).WithMessage(x => localizer["MaxLength"]);
    }
}
