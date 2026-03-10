using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Catalog.Commands.VendorProducts.UpdateVendorProduct;

public class UpdateVendorProductCommandValidator : AbstractValidator<UpdateVendorProductCommand>
{
    public UpdateVendorProductCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(v => v.Id)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .WithName("Id");

        RuleFor(v => v.VendorId)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .WithName("VendorId");

        RuleFor(v => v.SellingPrice)
            .GreaterThanOrEqualTo(0).WithMessage(localizer["MinValue"].Value)
            .WithName("SellingPrice");

        RuleFor(v => v.CompareAtPrice)
            .GreaterThanOrEqualTo(0).When(v => v.CompareAtPrice.HasValue).WithMessage(localizer["MinValue"].Value)
            .WithName("CompareAtPrice");

        RuleFor(v => v.StockQty)
            .GreaterThanOrEqualTo(0).WithMessage(localizer["MinValue"].Value)
            .WithName("StockQty");

        RuleFor(v => v.CustomNameAr)
            .MaximumLength(200).WithMessage(localizer["MaxLength"].Value)
            .WithName("CustomNameAr");

        RuleFor(v => v.CustomNameEn)
            .MaximumLength(200).WithMessage(localizer["MaxLength"].Value)
            .WithName("CustomNameEn");

        RuleFor(v => v.CustomDescriptionAr)
            .MaximumLength(1000).WithMessage(localizer["MaxLength"].Value)
            .WithName("CustomDescriptionAr");

        RuleFor(v => v.CustomDescriptionEn)
            .MaximumLength(1000).WithMessage(localizer["MaxLength"].Value)
            .WithName("CustomDescriptionEn");
    }
}
