namespace Zadana.Application.Modules.Finances.DTOs;

public class ZoneFinanceSettingsDto
{
    public Guid ZoneId { get; set; }
    public string ZoneName { get; set; } = null!;
    public string City { get; set; } = null!;
    
    // Pricing Rule
    public decimal BaseDeliveryFee { get; set; }
    public decimal IncludedKm { get; set; }
    public decimal ExtraKmFee { get; set; }
    public decimal MinDeliveryFee { get; set; }
    public decimal MaxDeliveryFee { get; set; }
    public bool IsPricingActive { get; set; }
    
    // Finance Settings
    public decimal VatPercent { get; set; }
    public string CodFeeType { get; set; } = null!; // "flat" or "percent"
    public decimal CodFlatFee { get; set; }
    public decimal CodPercent { get; set; }
    public bool IsVatActive { get; set; }
    public bool IsCodFeeActive { get; set; }
}
