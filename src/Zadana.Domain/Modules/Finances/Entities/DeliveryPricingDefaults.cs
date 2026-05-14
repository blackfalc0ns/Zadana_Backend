using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Finances.Entities;

public class DeliveryPricingDefaults : BaseEntity
{
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
    public decimal MinTotalDeliveryFee { get; private set; }
    public decimal MaxTotalDeliveryFee { get; private set; }
    public decimal MaxQuotedDistanceKm { get; private set; }
    public decimal WarningSubtotalRatioThreshold { get; private set; }

    private DeliveryPricingDefaults() { }

    public DeliveryPricingDefaults(
        Guid id,
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
        bool isCodFeeActive,
        decimal minTotalDeliveryFee,
        decimal maxTotalDeliveryFee,
        decimal maxQuotedDistanceKm,
        decimal warningSubtotalRatioThreshold)
    {
        Id = id;
        Update(
            baseDeliveryFee,
            includedKm,
            extraKmFee,
            minDeliveryFee,
            maxDeliveryFee,
            isPricingActive,
            vatPercent,
            codFeeType,
            codFlatFee,
            codPercent,
            isVatActive,
            isCodFeeActive,
            minTotalDeliveryFee,
            maxTotalDeliveryFee,
            maxQuotedDistanceKm,
            warningSubtotalRatioThreshold);
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
        bool isCodFeeActive,
        decimal minTotalDeliveryFee,
        decimal maxTotalDeliveryFee,
        decimal maxQuotedDistanceKm,
        decimal warningSubtotalRatioThreshold)
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
        MinTotalDeliveryFee = minTotalDeliveryFee;
        MaxTotalDeliveryFee = maxTotalDeliveryFee;
        MaxQuotedDistanceKm = maxQuotedDistanceKm;
        WarningSubtotalRatioThreshold = warningSubtotalRatioThreshold;
    }
}
