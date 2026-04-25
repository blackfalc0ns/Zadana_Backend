using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Delivery.Entities;

public class DeliveryPricingRule : BaseEntity
{
    public Guid? DeliveryZoneId { get; private set; }
    public string City { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public decimal BaseFee { get; private set; }
    public decimal IncludedKm { get; private set; }
    public decimal PerKmFee { get; private set; }
    public decimal MinFee { get; private set; }
    public decimal MaxFee { get; private set; }
    public bool IsActive { get; private set; }

    public DeliveryZone? DeliveryZone { get; private set; }
    public ICollection<DeliveryPricingSurgeWindow> SurgeWindows { get; private set; } = [];

    private DeliveryPricingRule() { }

    public DeliveryPricingRule(
        Guid? deliveryZoneId,
        string city,
        string name,
        decimal baseFee,
        decimal includedKm,
        decimal perKmFee,
        decimal minFee,
        decimal maxFee,
        bool isActive = true)
    {
        DeliveryZoneId = deliveryZoneId;
        City = city.Trim();
        Name = name.Trim();
        BaseFee = baseFee;
        IncludedKm = includedKm;
        PerKmFee = perKmFee;
        MinFee = minFee;
        MaxFee = maxFee;
        IsActive = isActive;
    }

    public void Update(
        Guid? deliveryZoneId,
        string city,
        string name,
        decimal baseFee,
        decimal includedKm,
        decimal perKmFee,
        decimal minFee,
        decimal maxFee,
        bool isActive)
    {
        DeliveryZoneId = deliveryZoneId;
        City = city.Trim();
        Name = name.Trim();
        BaseFee = baseFee;
        IncludedKm = includedKm;
        PerKmFee = perKmFee;
        MinFee = minFee;
        MaxFee = maxFee;
        IsActive = isActive;
    }
}
