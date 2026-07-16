using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Catalog.Enums;
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

    public async Task ApplyOrderCreationReservationAsync(
        IReadOnlyCollection<OrderItem> orderItems,
        IReadOnlyDictionary<Guid, VendorProduct> vendorProductsByMasterProductId,
        CancellationToken cancellationToken = default)
    {
        var itemsToDeduct = orderItems
            .Where(item => item.RequiresStockDeduction())
            .ToList();

        if (itemsToDeduct.Count == 0)
        {
            return;
        }

        var vendorProductsById = vendorProductsByMasterProductId.Values.ToDictionary(product => product.Id);
        var groupedItems = itemsToDeduct
            .GroupBy(item => item.VendorProductId)
            .Select(group =>
            {
                if (!vendorProductsById.TryGetValue(group.Key, out var vendorProduct))
                {
                    throw new BusinessRuleException("ORDER_ITEM_VENDOR_PRODUCT_MISSING", "One or more order items are missing their linked vendor product.");
                }

                return new StockDeductionGroup(
                    group.Key,
                    vendorProduct,
                    group.Sum(item => item.Quantity),
                    group.ToList());
            })
            .ToList();

        await ApplyStockDeductionAsync(
            groupedItems,
            "The order cannot be placed because one or more items are out of stock.",
            cancellationToken);
    }

    public Task ApplyExistingOrderReservationAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        ApplyPickupDeductionAsync(orderId, cancellationToken);

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
            .Select(group => new StockDeductionGroup(
                group.Key,
                group.First().VendorProduct,
                group.Sum(item => item.Quantity),
                group.ToList()))
            .ToList();

        await ApplyStockDeductionAsync(
            groupedItems,
            "The order cannot be handed off because one or more items are out of stock.",
            cancellationToken);
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

    private async Task ApplyStockDeductionAsync(
        IReadOnlyCollection<StockDeductionGroup> groupedItems,
        string insufficientStockMessage,
        CancellationToken cancellationToken)
    {
        if (groupedItems.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (CanUseAtomicStockUpdate())
        {
            foreach (var group in groupedItems)
            {
                var requiredQuantity = group.RequiredQuantity;
                var affectedRows = await _context.VendorProducts
                    .Where(product => product.Id == group.VendorProductId && product.StockQuantity >= requiredQuantity)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(product => product.StockQuantity, product => product.StockQuantity - requiredQuantity)
                        .SetProperty(product => product.IsAvailable, product => product.StockQuantity - requiredQuantity > 0)
                        .SetProperty(
                            product => product.Status,
                            product => product.StockQuantity - requiredQuantity == 0 && product.Status == VendorProductStatus.Active
                                ? VendorProductStatus.OutOfStock
                                : product.Status)
                        .SetProperty(product => product.UpdatedAtUtc, now),
                        cancellationToken);

                if (affectedRows == 0)
                {
                    throw new BusinessRuleException("INSUFFICIENT_STOCK", insufficientStockMessage);
                }

                MarkItemsStockDeducted(group.Items, now);
            }

            return;
        }

        var insufficientGroup = groupedItems
            .FirstOrDefault(group => group.VendorProduct.StockQuantity < group.RequiredQuantity);

        if (insufficientGroup is not null)
        {
            throw new BusinessRuleException("INSUFFICIENT_STOCK", insufficientStockMessage);
        }

        foreach (var group in groupedItems)
        {
            group.VendorProduct.DecreaseStock(group.RequiredQuantity);
            MarkItemsStockDeducted(group.Items, now);
        }
    }

    private bool CanUseAtomicStockUpdate()
    {
        if (_context is not DbContext dbContext)
        {
            return false;
        }

        return string.Equals(
            dbContext.Database.ProviderName,
            "Microsoft.EntityFrameworkCore.SqlServer",
            StringComparison.Ordinal);
    }

    private static void MarkItemsStockDeducted(IEnumerable<OrderItem> items, DateTime deductedAtUtc)
    {
        foreach (var item in items)
        {
            item.MarkStockDeducted(deductedAtUtc);
        }
    }

    private sealed record StockDeductionGroup(
        Guid VendorProductId,
        VendorProduct VendorProduct,
        int RequiredQuantity,
        List<OrderItem> Items);
}
