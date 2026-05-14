namespace Zadana.Application.Modules.Finances.DTOs;

public class DeliveryPricingDefaultsDto
{
    public Guid Id { get; set; }
    public string PricingScope { get; set; } = "global";
    public decimal BaseDeliveryFee { get; set; }
    public decimal IncludedKm { get; set; }
    public decimal ExtraKmFee { get; set; }
    public decimal MinDeliveryFee { get; set; }
    public decimal MaxDeliveryFee { get; set; }
    public bool IsPricingActive { get; set; }
    public decimal VatPercent { get; set; }
    public string CodFeeType { get; set; } = "flat";
    public decimal CodFlatFee { get; set; }
    public decimal CodPercent { get; set; }
    public bool IsVatActive { get; set; }
    public bool IsCodFeeActive { get; set; }
    public decimal MinTotalDeliveryFee { get; set; }
    public decimal MaxTotalDeliveryFee { get; set; }
    public decimal MaxQuotedDistanceKm { get; set; }
    public decimal WarningSubtotalRatioThreshold { get; set; }
}
