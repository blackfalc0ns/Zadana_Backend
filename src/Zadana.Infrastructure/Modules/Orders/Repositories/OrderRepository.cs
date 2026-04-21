using Microsoft.EntityFrameworkCore;
using Zadana.Application.Modules.Orders.Interfaces;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
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

    public async Task<Order?> GetReusablePendingOrderForCheckoutAsync(
        Guid userId,
        Guid vendorId,
        Guid customerAddressId,
        PaymentMethodType paymentMethod,
        Guid? vendorBranchId,
        Guid? couponId,
        string? notes,
        decimal subtotal,
        decimal discountTotal,
        decimal deliveryFee,
        decimal commissionAmount,
        IReadOnlyDictionary<Guid, int> itemQuantities,
        CancellationToken cancellationToken = default)
    {
        var normalizedNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

        var candidates = await _dbContext.Orders
            .Include(order => order.Items)
            .Where(order =>
                order.UserId == userId &&
                order.VendorId == vendorId &&
                order.CustomerAddressId == customerAddressId &&
                order.PaymentMethod == paymentMethod &&
                order.Status == OrderStatus.PendingPayment &&
                order.PaymentStatus != PaymentStatus.Paid &&
                order.VendorBranchId == vendorBranchId &&
                order.CouponId == couponId &&
                order.Subtotal == subtotal &&
                order.DiscountTotal == discountTotal &&
                order.DeliveryFee == deliveryFee &&
                order.CommissionAmount == commissionAmount)
            .OrderByDescending(order => order.PlacedAtUtc)
            .ToListAsync(cancellationToken);

        return candidates.FirstOrDefault(order =>
            string.Equals(order.Notes, normalizedNotes, StringComparison.Ordinal) &&
            HasMatchingItems(order, itemQuantities));
    }

    public void AddCart(Cart cart) => _dbContext.Carts.Add(cart);

    public void UpdateCart(Cart cart) => _dbContext.Carts.Update(cart);

    public void RemoveCart(Cart cart) => _dbContext.Carts.Remove(cart);

    public void AddOrder(Order order) => _dbContext.Orders.Add(order);

    public void AddOrderItem(OrderItem orderItem) => _dbContext.OrderItems.Add(orderItem);

    private static bool HasMatchingItems(Order order, IReadOnlyDictionary<Guid, int> itemQuantities)
    {
        var orderQuantities = order.Items
            .GroupBy(item => item.MasterProductId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));

        if (orderQuantities.Count != itemQuantities.Count)
        {
            return false;
        }

        foreach (var pair in itemQuantities)
        {
            if (!orderQuantities.TryGetValue(pair.Key, out var quantity) || quantity != pair.Value)
            {
                return false;
            }
        }

        return true;
    }
}
