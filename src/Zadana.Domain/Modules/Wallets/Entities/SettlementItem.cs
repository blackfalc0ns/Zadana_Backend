using Zadana.Domain.Modules.Orders.Entities;

namespace Zadana.Domain.Modules.Wallets.Entities;

public class SettlementItem
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid SettlementId { get; private set; }
    public Guid OrderId { get; private set; }
    
    public decimal VendorAmount { get; private set; }
    public decimal DriverAmount { get; private set; }
    public decimal PlatformCommission { get; private set; }
    public decimal CodCollectedAmount { get; private set; }

    // Navigation
    public Settlement Settlement { get; private set; } = null!;
    public Order Order { get; private set; } = null!;

    private SettlementItem() { }

    public SettlementItem(
        Guid settlementId, 
        Guid orderId, 
        decimal vendorAmount, 
        decimal driverAmount, 
        decimal platformCommission, 
        decimal codCollectedAmount)
    {
        SettlementId = settlementId;
        OrderId = orderId;
        VendorAmount = vendorAmount;
        DriverAmount = driverAmount;
        PlatformCommission = platformCommission;
        CodCollectedAmount = codCollectedAmount;
    }
}
