using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Catalog.DTOs;

namespace Zadana.Application.Modules.Catalog.Commands.VendorProducts.BulkCreateVendorProducts;

public record BulkCreateVendorProductItemInput(
    Guid MasterProductId,
    decimal SellingPrice,
    decimal? CompareAtPrice,
    int StockQty,
    Guid? BranchId,
    string? Sku,
    int MinOrderQty = 1,
    int? MaxOrderQty = null);

public record BulkCreateVendorProductsCommand(
    Guid VendorId,
    string IdempotencyKey,
    IReadOnlyList<BulkCreateVendorProductItemInput> Items) : MediatR.IRequest<VendorProductBulkOperationDto>;

public class BulkCreateVendorProductsCommandValidator : AbstractValidator<BulkCreateVendorProductsCommand>
{
    public BulkCreateVendorProductsCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.VendorId).NotEmpty().WithMessage(x => localizer["RequiredField"]);
        RuleFor(x => x.IdempotencyKey)
            .NotEmpty().WithMessage(x => localizer["RequiredField"])
            .MaximumLength(100).WithMessage(x => localizer["MaxLength"]);
        RuleFor(x => x.Items)
            .NotEmpty().WithMessage(x => localizer["RequiredField"]);

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.MasterProductId).NotEmpty().WithMessage(x => localizer["RequiredField"]);
            item.RuleFor(x => x.SellingPrice).GreaterThan(0).WithMessage(x => localizer["GreaterThanZero"]);
            item.RuleFor(x => x.CompareAtPrice)
                .GreaterThan(0).When(x => x.CompareAtPrice.HasValue).WithMessage(x => localizer["GreaterThanZero"]);
            item.RuleFor(x => x.StockQty)
                .GreaterThanOrEqualTo(0).WithMessage(x => localizer["MinValue"]);
            item.RuleFor(x => x.MinOrderQty)
                .GreaterThan(0).WithMessage(x => localizer["GreaterThanZero"]);
            item.RuleFor(x => x.MaxOrderQty)
                .GreaterThan(0).When(x => x.MaxOrderQty.HasValue).WithMessage(x => localizer["GreaterThanZero"])
                .GreaterThanOrEqualTo(x => x.MinOrderQty).When(x => x.MaxOrderQty.HasValue)
                .WithMessage(x => localizer["InvalidRange"]);
            item.RuleFor(x => x.Sku)
                .MaximumLength(100).WithMessage(x => localizer["MaxLength"]);
        });
    }
}
