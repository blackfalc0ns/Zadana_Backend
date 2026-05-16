using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Catalog.Commands;
using Zadana.Application.Modules.Catalog.Commands.UpdateMasterProduct;
using Zadana.Domain.Modules.Catalog.Enums;

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
    Guid? UnitId = null,
    Guid? PackageTypeId = null,
    decimal? MeasurementValue = null,
    Guid? MeasurementUnitId = null,
    Guid? VariantGroupId = null,
    ProductStatus Status = ProductStatus.Draft,
    List<CreateProductImageInfo>? Images = null) : MediatR.IRequest<Guid>
{
    public CreateMasterProductCommand(
        Guid categoryId,
        string nameAr,
        string nameEn,
        string slug,
        string? barcode,
        string? descriptionAr,
        string? descriptionEn,
        Guid? brandId,
        Guid? unitId,
        ProductStatus status,
        List<CreateProductImageInfo>? images = null)
        : this(categoryId, nameAr, nameEn, slug, barcode, descriptionAr, descriptionEn, brandId, unitId, null, null, null, null, status, images)
    {
    }
}

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

        RuleFor(x => x.MeasurementValue)
            .GreaterThan(0).When(x => x.MeasurementValue.HasValue)
            .WithMessage(x => localizer["GreaterThanZero"]);

        RuleFor(x => x)
            .Must(x => x.MeasurementValue.HasValue == x.ResolveMeasurementUnitId().HasValue)
            .WithMessage("Measurement value and measurement unit must be provided together.");
    }
}
