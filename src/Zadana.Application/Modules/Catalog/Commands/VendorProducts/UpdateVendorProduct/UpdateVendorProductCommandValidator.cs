using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Catalog.Commands.VendorProducts.UpdateVendorProduct;

public class UpdateVendorProductCommandValidator : AbstractValidator<UpdateVendorProductCommand>
{
    public UpdateVendorProductCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(v => v.Id)
            .NotEmpty().WithMessage(localizer["RequiredField", "Id"]);

        RuleFor(v => v.VendorId)
            .NotEmpty().WithMessage(localizer["RequiredField", "VendorId"]);

        RuleFor(v => v.SellingPrice)
            .GreaterThanOrEqualTo(0).WithMessage(localizer["MinValue", "SellingPrice"]);

        RuleFor(v => v.CompareAtPrice)
            .GreaterThanOrEqualTo(0).When(v => v.CompareAtPrice.HasValue).WithMessage(localizer["MinValue", "CompareAtPrice"]);

        RuleFor(v => v.StockQty)
            .GreaterThanOrEqualTo(0).WithMessage(localizer["MinValue", "StockQty"]);

        RuleFor(v => v.CustomNameAr)
            .MaximumLength(200).WithMessage(localizer["MaxLength", 200]);

        RuleFor(v => v.CustomNameEn)
            .MaximumLength(200).WithMessage(localizer["MaxLength", 200]);

        RuleFor(v => v.CustomDescriptionAr)
            .MaximumLength(1000).WithMessage(localizer["MaxLength", 1000]);

        RuleFor(v => v.CustomDescriptionEn)
            .MaximumLength(1000).WithMessage(localizer["MaxLength", 1000]);
    }
}
