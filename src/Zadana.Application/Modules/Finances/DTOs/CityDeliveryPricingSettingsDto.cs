namespace Zadana.Application.Modules.Finances.DTOs;

public class CityDeliveryPricingSettingsDto
{
    public Guid CityId { get; set; }
    public string CityCode { get; set; } = null!;
    public string CityNameAr { get; set; } = null!;
    public string CityNameEn { get; set; } = null!;
    public Guid RegionId { get; set; }
    public string RegionCode { get; set; } = null!;
    public string RegionNameAr { get; set; } = null!;
    public string RegionNameEn { get; set; } = null!;
    public string PricingScope { get; set; } = "city";
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
}
