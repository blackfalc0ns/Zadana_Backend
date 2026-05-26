using FluentValidation;
using MediatR;
using Zadana.Application.Modules.Finances.DTOs;

namespace Zadana.Application.Modules.Finances.Commands.UpdateDeliveryPricingDefaults;

public record UpdateDeliveryPricingDefaultsCommand(
    Guid Id,
    decimal BaseDeliveryFee,
    decimal IncludedKm,
    decimal ExtraKmFee,
    decimal MinDeliveryFee,
    decimal MaxDeliveryFee,
    bool IsPricingActive,
    decimal VatPercent,
    string CodFeeType,
    decimal CodFlatFee,
    decimal CodPercent,
    bool IsVatActive,
    bool IsCodFeeActive,
    decimal MinTotalDeliveryFee,
    decimal MaxTotalDeliveryFee,
    decimal MaxQuotedDistanceKm,
    decimal WarningSubtotalRatioThreshold) : IRequest<DeliveryPricingDefaultsDto>;

public sealed class UpdateDeliveryPricingDefaultsCommandValidator : AbstractValidator<UpdateDeliveryPricingDefaultsCommand>
{
    public UpdateDeliveryPricingDefaultsCommandValidator()
    {
        RuleFor(item => item.Id).NotEmpty();
        RuleFor(item => item.BaseDeliveryFee).GreaterThanOrEqualTo(0);
        RuleFor(item => item.IncludedKm).GreaterThanOrEqualTo(0);
        RuleFor(item => item.ExtraKmFee).GreaterThanOrEqualTo(0);
        RuleFor(item => item.MinDeliveryFee).GreaterThanOrEqualTo(0);
        RuleFor(item => item.MaxDeliveryFee).GreaterThanOrEqualTo(0);
        RuleFor(item => item.MinTotalDeliveryFee).GreaterThanOrEqualTo(0);
        RuleFor(item => item.MaxTotalDeliveryFee).GreaterThanOrEqualTo(0);
        RuleFor(item => item.MaxQuotedDistanceKm).GreaterThanOrEqualTo(0);
        RuleFor(item => item.WarningSubtotalRatioThreshold).InclusiveBetween(0, 1);
        RuleFor(item => item.VatPercent).InclusiveBetween(0, 100);
        RuleFor(item => item.CodFlatFee).GreaterThanOrEqualTo(0);
        RuleFor(item => item.CodPercent).InclusiveBetween(0, 100);
        RuleFor(item => item).Must(item => item.MaxDeliveryFee <= 0 || item.MaxDeliveryFee >= item.MinDeliveryFee)
            .WithMessage("MaxDeliveryFee must be zero or greater than or equal to MinDeliveryFee.");
        RuleFor(item => item).Must(item => item.MaxTotalDeliveryFee <= 0 || item.MaxTotalDeliveryFee >= item.MinTotalDeliveryFee)
            .WithMessage("MaxTotalDeliveryFee must be zero or greater than or equal to MinTotalDeliveryFee.");
        RuleFor(item => item.CodFeeType).NotEmpty().Must(item => item is "flat" or "percent");
    }
}
