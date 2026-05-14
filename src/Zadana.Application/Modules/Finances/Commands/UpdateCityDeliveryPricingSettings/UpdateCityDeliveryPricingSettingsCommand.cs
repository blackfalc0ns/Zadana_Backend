using FluentValidation;
using MediatR;
using Zadana.Application.Modules.Finances.DTOs;

namespace Zadana.Application.Modules.Finances.Commands.UpdateCityDeliveryPricingSettings;

public record UpdateCityDeliveryPricingSettingsCommand(
    Guid CityId,
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
    bool IsCodFeeActive) : IRequest<CityDeliveryPricingSettingsDto>;

public class UpdateCityDeliveryPricingSettingsCommandValidator : AbstractValidator<UpdateCityDeliveryPricingSettingsCommand>
{
    public UpdateCityDeliveryPricingSettingsCommandValidator()
    {
        RuleFor(x => x.CityId).NotEmpty();
        RuleFor(x => x.BaseDeliveryFee).GreaterThanOrEqualTo(0);
        RuleFor(x => x.IncludedKm).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ExtraKmFee).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MinDeliveryFee).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxDeliveryFee).GreaterThanOrEqualTo(0);
        RuleFor(x => x.VatPercent).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CodFlatFee).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CodPercent).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CodFeeType).Must(value => value is "flat" or "percent");
    }
}
