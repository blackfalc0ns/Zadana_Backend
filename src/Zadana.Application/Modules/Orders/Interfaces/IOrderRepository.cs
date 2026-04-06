using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Orders.Entities;

namespace Zadana.Application.Modules.Orders.Interfaces;

public interface IOrderRepository
{
    Task<MasterProduct?> GetMasterProductAsync(Guid masterProductId, CancellationToken cancellationToken = default);
    Task<Cart?> GetCartAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Cart?> GetCartForCheckoutAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, VendorProduct>> GetVendorProductsForCheckoutAsync(
        Guid vendorId,
        IReadOnlyCollection<Guid> masterProductIds,
        CancellationToken cancellationToken = default);
    void AddCart(Cart cart);
    void UpdateCart(Cart cart);
    void RemoveCart(Cart cart);
    void AddOrder(Order order);
    void AddOrderItem(OrderItem orderItem);
}
