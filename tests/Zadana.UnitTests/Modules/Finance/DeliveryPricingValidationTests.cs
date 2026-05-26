using FluentAssertions;
using Zadana.Application.Modules.Finances.Commands.UpdateDeliveryPricingDefaults;
using Zadana.Application.Modules.Finances.Commands.UpdateZoneFinanceSettings;

namespace Zadana.UnitTests.Modules.Finance;

public class DeliveryPricingValidationTests
{
    [Fact]
    public void DefaultsValidator_ShouldRejectInvalidAmountsAndPercentages()
    {
        var validator = new UpdateDeliveryPricingDefaultsCommandValidator();
        var result = validator.Validate(new UpdateDeliveryPricingDefaultsCommand(
            Guid.NewGuid(),
            BaseDeliveryFee: -1m,
            IncludedKm: 0m,
            ExtraKmFee: 1m,
            MinDeliveryFee: 50m,
            MaxDeliveryFee: 10m,
            IsPricingActive: true,
            VatPercent: 101m,
            CodFeeType: "flat",
            CodFlatFee: 0m,
            CodPercent: 0m,
            IsVatActive: true,
            IsCodFeeActive: true,
            MinTotalDeliveryFee: 20m,
            MaxTotalDeliveryFee: 10m,
            MaxQuotedDistanceKm: 0m,
            WarningSubtotalRatioThreshold: 1.5m));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(UpdateDeliveryPricingDefaultsCommand.BaseDeliveryFee));
        result.Errors.Should().Contain(error => error.PropertyName == nameof(UpdateDeliveryPricingDefaultsCommand.VatPercent));
    }

    [Fact]
    public void ZoneFinanceValidator_ShouldRejectInvalidCodPercent()
    {
        var validator = new UpdateZoneFinanceSettingsCommandValidator();
        var result = validator.Validate(new UpdateZoneFinanceSettingsCommand(
            Guid.NewGuid(),
            VatPercent: 15m,
            CodFeeType: "percent",
            CodFlatFee: 0m,
            CodPercent: 150m,
            IsVatActive: true,
            IsCodFeeActive: true));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(UpdateZoneFinanceSettingsCommand.CodPercent));
    }
}
