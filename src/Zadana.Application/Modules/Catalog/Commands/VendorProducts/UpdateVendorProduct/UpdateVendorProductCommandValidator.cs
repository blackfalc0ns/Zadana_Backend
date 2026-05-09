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

        RuleFor(v => v.CostPrice)
            .GreaterThanOrEqualTo(0).When(v => v.CostPrice.HasValue).WithMessage(localizer["MinValue"].Value)
            .WithName("CostPrice");

        RuleFor(v => v.TradePrice)
            .NotNull().WithMessage("Trade price is required.")
            .WithName("TradePrice");

        RuleFor(v => v.TradePrice)
            .GreaterThan(0).When(v => v.TradePrice.HasValue).WithMessage(localizer["GreaterThanZero"].Value)
            .WithName("TradePrice");

        RuleFor(v => v)
            .Must(v => !v.TradePrice.HasValue || v.TradePrice.Value <= v.SellingPrice)
            .WithMessage("Trade price must be less than or equal to selling price.")
            .WithName("TradePrice");

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
