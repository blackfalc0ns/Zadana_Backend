using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Wallets.Enums;

namespace Zadana.Domain.Modules.Wallets.Entities;

public class SettlementItem
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid SettlementId { get; private set; }
    public Guid? OrderId { get; private set; }
    public SettlementItemLineType LineType { get; private set; }
    public Guid SourceId { get; private set; }
    public Guid? WalletTransactionId { get; private set; }
    
    public decimal Amount { get; private set; }
    public decimal Commission { get; private set; }
    public decimal Refund { get; private set; }
    public decimal Adjustment { get; private set; }
    public decimal Recovery { get; private set; }
    public decimal NetAmount { get; private set; }

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
        decimal codCollectedAmount,
        Guid? walletTransactionId = null)
    {
        SettlementId = settlementId;
        OrderId = orderId;
        LineType = SettlementItemLineType.Order;
        SourceId = orderId;
        Amount = vendorAmount + driverAmount + platformCommission;
        Commission = platformCommission;
        Refund = 0;
        Adjustment = 0;
        Recovery = 0;
        NetAmount = vendorAmount;
        VendorAmount = vendorAmount;
        DriverAmount = driverAmount;
        PlatformCommission = platformCommission;
        CodCollectedAmount = codCollectedAmount;
        WalletTransactionId = walletTransactionId;
    }

    public SettlementItem(
        Guid settlementId,
        SettlementItemLineType lineType,
        Guid sourceId,
        Guid? orderId,
        decimal amount,
        decimal commission,
        decimal refund,
        decimal adjustment,
        decimal recovery,
        decimal netAmount,
        Guid? walletTransactionId = null)
    {
        SettlementId = settlementId;
        LineType = lineType;
        SourceId = sourceId;
        OrderId = orderId;
        Amount = amount;
        Commission = commission;
        Refund = refund;
        Adjustment = adjustment;
        Recovery = recovery;
        NetAmount = netAmount;
        VendorAmount = netAmount;
        DriverAmount = 0;
        PlatformCommission = commission;
        CodCollectedAmount = 0;
        WalletTransactionId = walletTransactionId;
    }

    public void ApplyVendorRecovery(decimal amount)
    {
        if (amount <= 0)
        {
            return;
        }

        if (VendorAmount < amount)
        {
            throw new InvalidOperationException("Settlement item vendor amount cannot absorb this recovery.");
        }

        VendorAmount -= amount;
        PlatformCommission += amount;
        Recovery += amount;
        NetAmount -= amount;
    }
}
