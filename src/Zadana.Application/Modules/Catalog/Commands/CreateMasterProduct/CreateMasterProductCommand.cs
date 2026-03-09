using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Catalog.Commands.CreateMasterProduct;

public record CreateMasterProductCommand(
    Guid CategoryId,
    string NameAr,
    string NameEn,
    string Slug,
    string? Barcode,
    string? DescriptionAr,
    string? DescriptionEn,
    Guid? BrandId,
    Guid? UnitId,
    List<CreateProductImageInfo>? Images = null) : MediatR.IRequest<Guid>;

public record CreateProductImageInfo(string Url, string? AltText, int DisplayOrder, bool IsPrimary);

public class CreateMasterProductCommandValidator : AbstractValidator<CreateMasterProductCommand>
{
    public CreateMasterProductCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.CategoryId).NotEmpty().WithMessage(x => localizer["RequiredField"]);

        RuleFor(x => x.NameAr)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(250).WithMessage(x => localizer["MaxLength"]);

        RuleFor(x => x.NameEn)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(250).WithMessage(x => localizer["MaxLength"]);

        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(250).WithMessage(x => localizer["MaxLength"]);

        RuleFor(x => x.Barcode)
            .MaximumLength(100).WithMessage(x => localizer["MaxLength"]);
    }
}
