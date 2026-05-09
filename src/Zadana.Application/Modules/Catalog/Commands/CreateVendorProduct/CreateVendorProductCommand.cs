using FluentValidation;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Catalog.Commands.CreateVendorProduct;

public record CreateVendorProductCommand(
    Guid VendorId,
    Guid MasterProductId,
    decimal SellingPrice,
    decimal? CompareAtPrice,
    decimal? CostPrice,
    decimal? TradePrice,
    int StockQty,
    int MinOrderQty,
    int? MaxOrderQty,
    string? Sku,
    Guid? BranchId) : MediatR.IRequest<Guid>;

public class CreateVendorProductCommandValidator : AbstractValidator<CreateVendorProductCommand>
{
    public CreateVendorProductCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.VendorId).NotEmpty().WithMessage(x => localizer["RequiredField"]);
        RuleFor(x => x.MasterProductId).NotEmpty().WithMessage(x => localizer["RequiredField"]);

        RuleFor(x => x.SellingPrice)
            .GreaterThan(0).WithMessage(x => localizer["GreaterThanZero"]);

        RuleFor(x => x.CompareAtPrice)
            .GreaterThan(0).When(x => x.CompareAtPrice.HasValue).WithMessage(x => localizer["GreaterThanZero"]);

        RuleFor(x => x.CostPrice)
            .GreaterThanOrEqualTo(0).When(x => x.CostPrice.HasValue).WithMessage(x => localizer["MinValue"]);

        RuleFor(x => x.TradePrice)
            .NotNull().WithMessage("Trade price is required.");

        RuleFor(x => x.TradePrice)
            .GreaterThan(0).When(x => x.TradePrice.HasValue).WithMessage(x => localizer["GreaterThanZero"]);

        RuleFor(x => x)
            .Must(x => !x.TradePrice.HasValue || x.TradePrice.Value <= x.SellingPrice)
            .WithMessage("Trade price must be less than or equal to selling price.");

        RuleFor(x => x.StockQty)
            .GreaterThanOrEqualTo(0).WithMessage(x => localizer["MinValue"]);

        RuleFor(x => x.MinOrderQty)
            .GreaterThan(0).WithMessage(x => localizer["GreaterThanZero"]);

        RuleFor(x => x.MaxOrderQty)
            .GreaterThan(0).When(x => x.MaxOrderQty.HasValue).WithMessage(x => localizer["GreaterThanZero"])
            .GreaterThanOrEqualTo(x => x.MinOrderQty).When(x => x.MaxOrderQty.HasValue)
            .WithMessage(x => localizer["InvalidRange"]);

        RuleFor(x => x.Sku)
            .MaximumLength(100).WithMessage(x => localizer["MaxLength"]);
    }
}
