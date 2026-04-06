using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Orders.Entities;

public class CartItem : BaseEntity
{
    public Guid CartId { get; private set; }
    public Guid MasterProductId { get; private set; }
    public int Quantity { get; private set; }
    public string ProductName { get; private set; } = null!;

    // Navigation
    public Cart Cart { get; private set; } = null!;
    public MasterProduct MasterProduct { get; private set; } = null!;

    private CartItem() { }

    public CartItem(Guid cartId, Guid masterProductId, string productName, int quantity)
    {
        if (quantity <= 0) throw new BusinessRuleException("INVALID_QUANTITY", "Quantity must be greater than zero.");

        CartId = cartId;
        MasterProductId = masterProductId;
        ProductName = productName.Trim();
        Quantity = quantity;
    }

    public void UpdateQuantity(int quantity)
    {
        if (quantity <= 0) throw new BusinessRuleException("INVALID_QUANTITY", "Quantity must be greater than zero.");
        Quantity = quantity;
    }
}
