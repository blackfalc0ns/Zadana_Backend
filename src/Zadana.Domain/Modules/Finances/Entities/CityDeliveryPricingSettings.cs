using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Finances.Entities;

public class CityDeliveryPricingSettings : BaseEntity
{
    public Guid SaudiCityId { get; private set; }
    public decimal BaseDeliveryFee { get; private set; }
    public decimal IncludedKm { get; private set; }
    public decimal ExtraKmFee { get; private set; }
    public decimal MinDeliveryFee { get; private set; }
    public decimal MaxDeliveryFee { get; private set; }
    public bool IsPricingActive { get; private set; }
    public decimal VatPercent { get; private set; }
    public string CodFeeType { get; private set; } = null!;
    public decimal CodFlatFee { get; private set; }
    public decimal CodPercent { get; private set; }
    public bool IsVatActive { get; private set; }
    public bool IsCodFeeActive { get; private set; }

    private CityDeliveryPricingSettings() { }

    public CityDeliveryPricingSettings(
        Guid saudiCityId,
        decimal baseDeliveryFee,
        decimal includedKm,
        decimal extraKmFee,
        decimal minDeliveryFee,
        decimal maxDeliveryFee,
        bool isPricingActive,
        decimal vatPercent,
        string codFeeType,
        decimal codFlatFee,
        decimal codPercent,
        bool isVatActive = true,
        bool isCodFeeActive = true)
    {
        SaudiCityId = saudiCityId;
        BaseDeliveryFee = baseDeliveryFee;
        IncludedKm = includedKm;
        ExtraKmFee = extraKmFee;
        MinDeliveryFee = minDeliveryFee;
        MaxDeliveryFee = maxDeliveryFee;
        IsPricingActive = isPricingActive;
        VatPercent = vatPercent;
        CodFeeType = codFeeType.Trim().ToLowerInvariant();
        CodFlatFee = codFlatFee;
        CodPercent = codPercent;
        IsVatActive = isVatActive;
        IsCodFeeActive = isCodFeeActive;
    }

    public void Update(
        decimal baseDeliveryFee,
        decimal includedKm,
        decimal extraKmFee,
        decimal minDeliveryFee,
        decimal maxDeliveryFee,
        bool isPricingActive,
        decimal vatPercent,
        string codFeeType,
        decimal codFlatFee,
        decimal codPercent,
        bool isVatActive,
        bool isCodFeeActive)
    {
        BaseDeliveryFee = baseDeliveryFee;
        IncludedKm = includedKm;
        ExtraKmFee = extraKmFee;
        MinDeliveryFee = minDeliveryFee;
        MaxDeliveryFee = maxDeliveryFee;
        IsPricingActive = isPricingActive;
        VatPercent = vatPercent;
        CodFeeType = codFeeType.Trim().ToLowerInvariant();
        CodFlatFee = codFlatFee;
        CodPercent = codPercent;
        IsVatActive = isVatActive;
        IsCodFeeActive = isCodFeeActive;
    }
}
