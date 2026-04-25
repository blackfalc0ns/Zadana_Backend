using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Delivery.Entities;

public class DeliveryPricingSurgeWindow : BaseEntity
{
    public Guid DeliveryPricingRuleId { get; private set; }
    public string Name { get; private set; } = null!;
    public TimeSpan StartLocalTime { get; private set; }
    public TimeSpan EndLocalTime { get; private set; }
    public decimal Multiplier { get; private set; }
    public bool IsActive { get; private set; }

    public DeliveryPricingRule DeliveryPricingRule { get; private set; } = null!;

    private DeliveryPricingSurgeWindow() { }

    public DeliveryPricingSurgeWindow(
        Guid deliveryPricingRuleId,
        string name,
        TimeSpan startLocalTime,
        TimeSpan endLocalTime,
        decimal multiplier,
        bool isActive = true)
    {
        DeliveryPricingRuleId = deliveryPricingRuleId;
        Name = name.Trim();
        StartLocalTime = startLocalTime;
        EndLocalTime = endLocalTime;
        Multiplier = multiplier;
        IsActive = isActive;
    }

    public void Update(
        string name,
        TimeSpan startLocalTime,
        TimeSpan endLocalTime,
        decimal multiplier,
        bool isActive)
    {
        Name = name.Trim();
        StartLocalTime = startLocalTime;
        EndLocalTime = endLocalTime;
        Multiplier = multiplier;
        IsActive = isActive;
    }
}
