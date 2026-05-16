using FluentValidation;
using MediatR;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Catalog.Commands;
using Zadana.Application.Modules.Catalog.Commands.CreateMasterProduct;
using Zadana.Domain.Modules.Catalog.Enums;

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
    Guid? UnitId = null,
    Guid? PackageTypeId = null,
    decimal? MeasurementValue = null,
    Guid? MeasurementUnitId = null,
    Guid? VariantGroupId = null,
    ProductStatus? Status = null,
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
        RuleFor(x => x.MeasurementValue)
            .GreaterThan(0).When(x => x.MeasurementValue.HasValue)
            .WithMessage(x => localizer["GreaterThanZero"]);
        RuleFor(x => x)
            .Must(x => x.MeasurementValue.HasValue == x.ResolveMeasurementUnitId().HasValue)
            .WithMessage("Measurement value and measurement unit must be provided together.");
    }
}
