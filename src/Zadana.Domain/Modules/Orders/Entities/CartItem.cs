using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Orders.Entities;

public class CartItem : BaseEntity
{
    public Guid CartId { get; private set; }
    public Guid VendorProductId { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal LineTotal { get; private set; }

    // Navigation
    public Cart Cart { get; private set; } = null!;
    public VendorProduct VendorProduct { get; private set; } = null!;

    private CartItem() { }

    public CartItem(Guid cartId, Guid vendorProductId, int quantity, decimal unitPrice)
    {
        if (quantity <= 0) throw new BusinessRuleException("INVALID_QUANTITY", "Quantity must be greater than zero.");
        if (unitPrice < 0) throw new BusinessRuleException("INVALID_PRICE", "Unit price cannot be negative.");

        CartId = cartId;
        VendorProductId = vendorProductId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        RecalculateLineTotal();
    }

    public void UpdateQuantity(int quantity)
    {
        if (quantity <= 0) throw new BusinessRuleException("INVALID_QUANTITY", "Quantity must be greater than zero.");
        Quantity = quantity;
        RecalculateLineTotal();
    }

    public void UpdatePrice(decimal unitPrice)
    {
        if (unitPrice < 0) throw new BusinessRuleException("INVALID_PRICE", "Unit price cannot be negative.");
        UnitPrice = unitPrice;
        RecalculateLineTotal();
    }

    private void RecalculateLineTotal()
    {
        LineTotal = Quantity * UnitPrice;
    }
}
