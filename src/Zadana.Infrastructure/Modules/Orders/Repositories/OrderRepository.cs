using Microsoft.EntityFrameworkCore;
using Zadana.Application.Modules.Orders.Interfaces;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Infrastructure.Persistence;

namespace Zadana.Infrastructure.Modules.Orders.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly ApplicationDbContext _dbContext;

    public OrderRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<MasterProduct?> GetMasterProductAsync(Guid masterProductId, CancellationToken cancellationToken = default) =>
        _dbContext.MasterProducts.FirstOrDefaultAsync(product => product.Id == masterProductId, cancellationToken);

    public Task<Cart?> GetCartAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _dbContext.Carts
            .Include(cart => cart.Items)
            .FirstOrDefaultAsync(cart => cart.UserId == userId, cancellationToken);

    public Task<Cart?> GetCartForCheckoutAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _dbContext.Carts
            .Include(cart => cart.Items)
                .ThenInclude(item => item.MasterProduct)
            .FirstOrDefaultAsync(cart => cart.UserId == userId, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, VendorProduct>> GetVendorProductsForCheckoutAsync(
        Guid vendorId,
        IReadOnlyCollection<Guid> masterProductIds,
        CancellationToken cancellationToken = default)
    {
        var vendorProducts = await _dbContext.VendorProducts
            .Include(product => product.MasterProduct)
            .Where(product => product.VendorId == vendorId && masterProductIds.Contains(product.MasterProductId))
            .ToListAsync(cancellationToken);

        return vendorProducts.ToDictionary(product => product.MasterProductId);
    }

    public void AddCart(Cart cart) => _dbContext.Carts.Add(cart);

    public void UpdateCart(Cart cart) => _dbContext.Carts.Update(cart);

    public void RemoveCart(Cart cart) => _dbContext.Carts.Remove(cart);

    public void AddOrder(Order order) => _dbContext.Orders.Add(order);

    public void AddOrderItem(OrderItem orderItem) => _dbContext.OrderItems.Add(orderItem);
}
