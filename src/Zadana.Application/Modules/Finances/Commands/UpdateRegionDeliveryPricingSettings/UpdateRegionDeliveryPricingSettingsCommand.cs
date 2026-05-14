using FluentValidation;
using MediatR;
using Zadana.Application.Modules.Finances.DTOs;

namespace Zadana.Application.Modules.Finances.Commands.UpdateRegionDeliveryPricingSettings;

public record UpdateRegionDeliveryPricingSettingsCommand(
    Guid RegionId,
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
    bool IsCodFeeActive) : IRequest<RegionDeliveryPricingSettingsDto>;

public sealed class UpdateRegionDeliveryPricingSettingsCommandValidator : AbstractValidator<UpdateRegionDeliveryPricingSettingsCommand>
{
    public UpdateRegionDeliveryPricingSettingsCommandValidator()
    {
        RuleFor(item => item.RegionId).NotEmpty();
        RuleFor(item => item.CodFeeType).NotEmpty().Must(item => item is "flat" or "percent");
    }
}
