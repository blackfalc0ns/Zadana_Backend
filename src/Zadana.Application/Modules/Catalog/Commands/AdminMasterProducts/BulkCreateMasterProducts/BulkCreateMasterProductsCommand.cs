using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Domain.Modules.Catalog.Enums;

namespace Zadana.Application.Modules.Catalog.Commands.AdminMasterProducts.BulkCreateMasterProducts;

public record BulkCreateMasterProductItemInput(
    string NameAr,
    string NameEn,
    string? Slug,
    string? Barcode,
    Guid CategoryId,
    Guid? BrandId,
    Guid? UnitId,
    ProductStatus Status,
    string? DescriptionAr,
    string? DescriptionEn);

public record BulkCreateMasterProductsCommand(
    Guid AdminUserId,
    string IdempotencyKey,
    IReadOnlyList<BulkCreateMasterProductItemInput> Items) : MediatR.IRequest<AdminMasterProductBulkOperationDto>;

public class BulkCreateMasterProductsCommandValidator : AbstractValidator<BulkCreateMasterProductsCommand>
{
    public BulkCreateMasterProductsCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.AdminUserId).NotEmpty().WithMessage(x => localizer["RequiredField"]);
        RuleFor(x => x.IdempotencyKey)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(100).WithMessage(x => localizer["MaxLength"]);
        RuleFor(x => x.Items)
            .NotEmpty().WithMessage(x => localizer["RequiredField"]);

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.NameAr)
                .NotEmpty().WithMessage(x => localizer["RequiredField"])
                .MaximumLength(250).WithMessage(x => localizer["MaxLength"]);
            item.RuleFor(x => x.NameEn)
                .NotEmpty().WithMessage(x => localizer["RequiredField"])
                .MaximumLength(250).WithMessage(x => localizer["MaxLength"]);
            item.RuleFor(x => x.Slug)
                .MaximumLength(250).When(x => !string.IsNullOrWhiteSpace(x.Slug)).WithMessage(x => localizer["MaxLength"]);
            item.RuleFor(x => x.Barcode)
                .MaximumLength(100).When(x => !string.IsNullOrWhiteSpace(x.Barcode)).WithMessage(x => localizer["MaxLength"]);
            item.RuleFor(x => x.CategoryId)
                .NotEmpty().WithMessage(x => localizer["RequiredField"]);
        });
    }
}
