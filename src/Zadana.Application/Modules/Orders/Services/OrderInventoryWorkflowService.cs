using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Orders.Services;

public sealed class OrderInventoryWorkflowService
{
    private readonly IApplicationDbContext _context;

    public OrderInventoryWorkflowService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task ApplyPickupDeductionAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var items = await LoadOrderItemsAsync(orderId, cancellationToken);
        var itemsToDeduct = items
            .Where(item => item.RequiresStockDeduction())
            .ToList();

        if (itemsToDeduct.Count == 0)
        {
            return;
        }

        EnsureVendorProductsLoaded(itemsToDeduct);

        var groupedItems = itemsToDeduct
            .GroupBy(item => item.VendorProductId)
            .Select(group => new
            {
                VendorProduct = group.First().VendorProduct,
                RequiredQuantity = group.Sum(item => item.Quantity),
                Items = group.ToList()
            })
            .ToList();

        var insufficientGroup = groupedItems
            .FirstOrDefault(group => group.VendorProduct.StockQuantity < group.RequiredQuantity);

        if (insufficientGroup is not null)
        {
            throw new BusinessRuleException("INSUFFICIENT_STOCK", "The order cannot be handed off because one or more items are out of stock.");
        }

        var now = DateTime.UtcNow;
        foreach (var group in groupedItems)
        {
            group.VendorProduct.DecreaseStock(group.RequiredQuantity);
            foreach (var item in group.Items)
            {
                item.MarkStockDeducted(now);
            }
        }
    }

    public async Task ApplyRestockAsync(Guid orderId, string reason, CancellationToken cancellationToken = default)
    {
        _ = reason;

        var items = await LoadOrderItemsAsync(orderId, cancellationToken);
        var itemsToRestore = items
            .Where(item => item.RequiresStockRestore())
            .ToList();

        if (itemsToRestore.Count == 0)
        {
            return;
        }

        EnsureVendorProductsLoaded(itemsToRestore);

        var groupedItems = itemsToRestore
            .GroupBy(item => item.VendorProductId)
            .Select(group => new
            {
                VendorProduct = group.First().VendorProduct,
                RestoreQuantity = group.Sum(item => item.Quantity),
                Items = group.ToList()
            })
            .ToList();

        var now = DateTime.UtcNow;
        foreach (var group in groupedItems)
        {
            group.VendorProduct.IncreaseStock(group.RestoreQuantity);
            foreach (var item in group.Items)
            {
                item.MarkStockRestored(now);
            }
        }
    }

    private async Task<List<OrderItem>> LoadOrderItemsAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var items = await _context.OrderItems
            .Include(item => item.VendorProduct)
            .Where(item => item.OrderId == orderId)
            .ToListAsync(cancellationToken);

        return items;
    }

    private static void EnsureVendorProductsLoaded(IEnumerable<OrderItem> items)
    {
        if (items.Any(item => item.VendorProduct is null))
        {
            throw new BusinessRuleException("ORDER_ITEM_VENDOR_PRODUCT_MISSING", "One or more order items are missing their linked vendor product.");
        }
    }
}
