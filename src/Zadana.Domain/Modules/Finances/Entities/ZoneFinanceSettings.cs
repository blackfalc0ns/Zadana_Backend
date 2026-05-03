using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Finances.Entities;

public class ZoneFinanceSettings : BaseEntity
{
    public Guid DeliveryZoneId { get; private set; }
    public decimal VatPercent { get; private set; }
    public string CodFeeType { get; private set; } = null!; // "flat" or "percent"
    public decimal CodFlatFee { get; private set; }
    public decimal CodPercent { get; private set; }
    public bool IsVatActive { get; private set; }
    public bool IsCodFeeActive { get; private set; }

    private ZoneFinanceSettings() { }

    public ZoneFinanceSettings(
        Guid deliveryZoneId,
        decimal vatPercent,
        string codFeeType,
        decimal codFlatFee,
        decimal codPercent,
        bool isVatActive = true,
        bool isCodFeeActive = true)
    {
        DeliveryZoneId = deliveryZoneId;
        VatPercent = vatPercent;
        CodFeeType = codFeeType;
        CodFlatFee = codFlatFee;
        CodPercent = codPercent;
        IsVatActive = isVatActive;
        IsCodFeeActive = isCodFeeActive;
    }

    public void Update(
        decimal vatPercent,
        string codFeeType,
        decimal codFlatFee,
        decimal codPercent,
        bool isVatActive,
        bool isCodFeeActive)
    {
        VatPercent = vatPercent;
        CodFeeType = codFeeType;
        CodFlatFee = codFlatFee;
        CodPercent = codPercent;
        IsVatActive = isVatActive;
        IsCodFeeActive = isCodFeeActive;
    }
}
