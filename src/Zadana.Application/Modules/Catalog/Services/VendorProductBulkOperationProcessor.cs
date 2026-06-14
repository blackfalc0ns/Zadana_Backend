using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.Interfaces;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.Domain.Modules.Vendors.Enums;

namespace Zadana.Application.Modules.Catalog.Services;

public sealed class VendorProductBulkOperationProcessor : IVendorProductBulkOperationProcessor
{
    private static readonly TimeSpan ProcessingRecoveryAge = TimeSpan.FromMinutes(5);
    private readonly IApplicationDbContext _context;

    public VendorProductBulkOperationProcessor(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task ProcessOperationAsync(Guid operationId, CancellationToken cancellationToken = default)
    {
        var operation = await _context.VendorProductBulkOperations
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == operationId, cancellationToken);

        if (operation is null || IsTerminal(operation.Status))
        {
            return;
        }

        if (operation.Status == VendorProductBulkOperationStatus.Processing &&
            operation.StartedAtUtc.HasValue &&
            operation.StartedAtUtc.Value > DateTime.UtcNow.Subtract(ProcessingRecoveryAge))
        {
            return;
        }

        operation.MarkProcessing();
        await _context.SaveChangesAsync(cancellationToken);

        var vendor = await _context.Vendors
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == operation.VendorId, cancellationToken);

        if (vendor is null)
        {
            operation.MarkFailed("Vendor was not found.");
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        if (vendor.Status != VendorStatus.Active)
        {
            operation.MarkFailed("Vendor is not active.");
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        var masterProductIds = operation.Items.Select(x => x.MasterProductId).Distinct().ToArray();
        var existingMasterProductIds = await _context.MasterProducts
            .AsNoTracking()
            .Where(x => masterProductIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToHashSetAsync(cancellationToken);

        var existingVendorProductKeys = await _context.VendorProducts
            .AsNoTracking()
            .Where(x => x.VendorId == operation.VendorId && masterProductIds.Contains(x.MasterProductId))
            .Select(x => new VendorProductScopeKey(x.MasterProductId, x.VendorBranchId))
            .ToListAsync(cancellationToken);

        var existingVendorProductKeySet = existingVendorProductKeys.ToHashSet();

        var existingPricingRows = await _context.VendorProducts
            .AsNoTracking()
            .Where(x => x.VendorId == operation.VendorId && masterProductIds.Contains(x.MasterProductId))
            .Select(product => new CanonicalVendorProductPricing(
                product.MasterProductId,
                product.SellingPrice,
                product.CompareAtPrice,
                product.CostPrice,
                product.TradePrice,
                product.VendorBranchId,
                product.VendorBranch != null && product.VendorBranch.IsPrimary,
                product.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        var canonicalPricingByProductId = existingPricingRows
            .GroupBy(x => x.MasterProductId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(product => product.IsPrimaryBranch)
                    .ThenBy(product => product.VendorBranchId.HasValue)
                    .ThenBy(product => product.CreatedAtUtc)
                    .First());

        var branchScopes = await _context.VendorBranches
            .AsNoTracking()
            .Where(x => x.VendorId == operation.VendorId)
            .Select(x => new { x.Id, x.IsPrimary })
            .ToListAsync(cancellationToken);
        var validBranchIds = branchScopes.Select(x => x.Id).ToHashSet();
        var primaryBranchIds = branchScopes
            .Where(x => x.IsPrimary)
            .Select(x => x.Id)
            .ToHashSet();

        foreach (var item in operation.Items.OrderBy(x => x.RowNumber))
        {
            if (item.Status != VendorProductBulkOperationItemStatus.Pending)
            {
                continue;
            }

            if (!existingMasterProductIds.Contains(item.MasterProductId))
            {
                item.MarkFailed("Master product was not found.");
            }
            else if (existingVendorProductKeySet.Contains(new VendorProductScopeKey(item.MasterProductId, item.VendorBranchId)))
            {
                item.MarkSkipped("Product already exists in vendor branch.");
            }
            else if (item.VendorBranchId.HasValue && !validBranchIds.Contains(item.VendorBranchId.Value))
            {
                item.MarkFailed("Branch is invalid for this vendor.");
            }
            else if (item.CompareAtPrice.HasValue && item.CompareAtPrice.Value <= item.SellingPrice)
            {
                item.MarkFailed("Compare price must be greater than selling price.");
            }
            else if (!item.TradePrice.HasValue)
            {
                item.MarkFailed("Trade price is required.");
            }
            else if (item.TradePrice.HasValue && item.TradePrice.Value > item.SellingPrice)
            {
                item.MarkFailed("Trade price must be less than or equal to selling price.");
            }
            else if (item.MinOrderQty <= 0)
            {
                item.MarkFailed("Minimum order quantity must be greater than zero.");
            }
            else if (item.MaxOrderQty.HasValue && item.MaxOrderQty.Value < item.MinOrderQty)
            {
                item.MarkFailed("Maximum order quantity must be greater than or equal to minimum order quantity.");
            }
            else
            {
                await CreateVendorProductAsync(operation, item, existingVendorProductKeySet, canonicalPricingByProductId, primaryBranchIds, cancellationToken);
            }

            operation.RecalculateProgress();
            await _context.SaveChangesAsync(cancellationToken);
        }

        operation.RecalculateProgress();
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task CreateVendorProductAsync(
        VendorProductBulkOperation operation,
        VendorProductBulkOperationItem item,
        HashSet<VendorProductScopeKey> existingVendorProductKeySet,
        Dictionary<Guid, CanonicalVendorProductPricing> canonicalPricingByProductId,
        HashSet<Guid> primaryBranchIds,
        CancellationToken cancellationToken)
    {
        VendorProduct? vendorProduct = null;

        try
        {
            canonicalPricingByProductId.TryGetValue(item.MasterProductId, out var canonicalPricing);
            var isCanonicalPriceScope = !item.VendorBranchId.HasValue ||
                primaryBranchIds.Contains(item.VendorBranchId.Value);

            var sellingPrice = isCanonicalPriceScope ? item.SellingPrice : canonicalPricing?.SellingPrice ?? item.SellingPrice;
            var compareAtPrice = isCanonicalPriceScope ? item.CompareAtPrice : canonicalPricing?.CompareAtPrice ?? item.CompareAtPrice;
            var costPrice = isCanonicalPriceScope ? null : canonicalPricing?.CostPrice;
            var tradePrice = isCanonicalPriceScope ? item.TradePrice : canonicalPricing?.TradePrice ?? item.TradePrice;

            if (isCanonicalPriceScope)
            {
                var existingProductRows = await _context.VendorProducts
                    .Where(product =>
                        product.VendorId == operation.VendorId &&
                        product.MasterProductId == item.MasterProductId)
                    .ToListAsync(cancellationToken);

                foreach (var productRow in existingProductRows)
                {
                    productRow.UpdatePricing(sellingPrice, compareAtPrice, costPrice, tradePrice);
                }
            }

            vendorProduct = new VendorProduct(
                operation.VendorId,
                item.MasterProductId,
                sellingPrice,
                item.StockQty,
                compareAtPrice,
                costPrice,
                tradePrice,
                item.VendorBranchId);

            _context.VendorProducts.Add(vendorProduct);
            await _context.SaveChangesAsync(cancellationToken);

            item.MarkSucceeded(vendorProduct.Id);
            existingVendorProductKeySet.Add(new VendorProductScopeKey(item.MasterProductId, item.VendorBranchId));
            canonicalPricingByProductId[item.MasterProductId] = new CanonicalVendorProductPricing(
                item.MasterProductId,
                vendorProduct.SellingPrice,
                vendorProduct.CompareAtPrice,
                vendorProduct.CostPrice,
                vendorProduct.TradePrice,
                vendorProduct.VendorBranchId,
                isCanonicalPriceScope,
                vendorProduct.CreatedAtUtc);
        }
        catch (DbUpdateException)
        {
            if (vendorProduct is not null)
            {
                DetachIfPossible(vendorProduct);
            }

            item.MarkSkipped("Product already exists in vendor branch.");
        }
        catch (Exception ex)
        {
            item.MarkFailed(ex.Message);
        }
    }

    private static bool IsTerminal(VendorProductBulkOperationStatus status)
        => status is VendorProductBulkOperationStatus.Completed
            or VendorProductBulkOperationStatus.CompletedWithErrors
            or VendorProductBulkOperationStatus.Failed;

    private void DetachIfPossible(object entity)
    {
        if (_context is DbContext dbContext)
        {
            dbContext.Entry(entity).State = EntityState.Detached;
        }
    }

    private sealed record VendorProductScopeKey(Guid MasterProductId, Guid? VendorBranchId);

    private sealed record CanonicalVendorProductPricing(
        Guid MasterProductId,
        decimal SellingPrice,
        decimal? CompareAtPrice,
        decimal? CostPrice,
        decimal? TradePrice,
        Guid? VendorBranchId,
        bool IsPrimaryBranch,
        DateTime CreatedAtUtc);
}
