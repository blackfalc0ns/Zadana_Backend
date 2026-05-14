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
        RuleFor(item => item.CodFeeType).NotEmpty().Must(item => item is "flat" or "percent");
    }
}
