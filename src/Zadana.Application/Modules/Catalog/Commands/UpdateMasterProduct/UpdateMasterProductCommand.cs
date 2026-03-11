using FluentValidation;
using MediatR;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Catalog.Commands.CreateMasterProduct;

namespace Zadana.Application.Modules.Catalog.Commands.UpdateMasterProduct;

public record UpdateMasterProductCommand(
    Guid Id,
    Guid CategoryId,
    string NameAr,
    string NameEn,
    string Slug,
    string? Barcode,
    string? DescriptionAr,
    string? DescriptionEn,
    Guid? BrandId,
    Guid? UnitId,
    List<CreateProductImageInfo>? Images = null) : IRequest<Unit>;

public class UpdateMasterProductCommandValidator : AbstractValidator<UpdateMasterProductCommand>
{
    public UpdateMasterProductCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.CategoryId).NotEmpty().WithMessage(x => localizer["RequiredField"]);
        RuleFor(x => x.NameAr).NotEmpty().WithMessage(x => localizer["RequiredField"]).MaximumLength(250);
        RuleFor(x => x.NameEn).NotEmpty().WithMessage(x => localizer["RequiredField"]).MaximumLength(250);
        RuleFor(x => x.Slug).NotEmpty().WithMessage(x => localizer["RequiredField"]).MaximumLength(250);
    }
}
