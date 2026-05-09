using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Domain.Modules.Orders.Entities;

public class OrderItem
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid OrderId { get; private set; }
    public Guid VendorProductId { get; private set; }
    public Guid MasterProductId { get; private set; }
    public string ProductName { get; private set; } = null!;
    public string? UnitName { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal? TradeUnitPrice { get; private set; }
    public decimal VendorProfitPerUnit { get; private set; }
    public decimal LineDiscount { get; private set; }
    public decimal LineTotal { get; private set; }

    // Navigation
    public Order Order { get; private set; } = null!;
    public VendorProduct VendorProduct { get; private set; } = null!;
    public MasterProduct MasterProduct { get; private set; } = null!;

    private OrderItem() { }

    public OrderItem(
        Guid orderId, 
        Guid vendorProductId, 
        Guid masterProductId, 
        string productName, 
        int quantity, 
        decimal unitPrice,
        decimal? tradeUnitPrice = null,
        decimal vendorProfitPerUnit = 0,
        decimal lineDiscount = 0,
        string? unitName = null)
    {
        if (quantity <= 0) throw new BusinessRuleException("INVALID_QUANTITY", "Quantity must be greater than zero.");
        if (unitPrice < 0) throw new BusinessRuleException("INVALID_PRICE", "Unit price cannot be negative.");
        if (tradeUnitPrice.HasValue && tradeUnitPrice.Value <= 0) throw new BusinessRuleException("INVALID_TRADE_PRICE", "Trade unit price must be greater than zero.");
        if (vendorProfitPerUnit < 0) throw new BusinessRuleException("INVALID_VENDOR_PROFIT", "Vendor profit per unit cannot be negative.");

        OrderId = orderId;
        VendorProductId = vendorProductId;
        MasterProductId = masterProductId;
        ProductName = productName.Trim();
        Quantity = quantity;
        UnitPrice = unitPrice;
        TradeUnitPrice = tradeUnitPrice;
        VendorProfitPerUnit = vendorProfitPerUnit;
        LineDiscount = lineDiscount;
        UnitName = unitName?.Trim();
        LineTotal = Math.Max(0, (quantity * unitPrice) - lineDiscount);
    }
}
