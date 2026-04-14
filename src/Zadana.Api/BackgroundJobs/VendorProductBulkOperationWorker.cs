using Microsoft.EntityFrameworkCore;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.Application.Modules.Catalog.Interfaces;
using Zadana.Infrastructure.Persistence;

namespace Zadana.Api.BackgroundJobs;

public sealed class VendorProductBulkOperationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IVendorProductBulkOperationQueue _queue;
    private readonly ILogger<VendorProductBulkOperationWorker> _logger;

    public VendorProductBulkOperationWorker(
        IServiceScopeFactory scopeFactory,
        IVendorProductBulkOperationQueue queue,
        ILogger<VendorProductBulkOperationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var operationId = await _queue.DequeueAsync(stoppingToken);

            try
            {
                await ProcessOperationAsync(operationId, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process vendor product bulk operation {OperationId}", operationId);
            }
        }
    }

    private async Task ProcessOperationAsync(Guid operationId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var operation = await context.VendorProductBulkOperations
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == operationId, cancellationToken);

        if (operation is null)
        {
            return;
        }

        operation.MarkProcessing();
        await context.SaveChangesAsync(cancellationToken);

        var vendor = await context.Vendors
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == operation.VendorId, cancellationToken);

        if (vendor is null)
        {
            operation.MarkFailed("Vendor was not found.");
            await context.SaveChangesAsync(cancellationToken);
            return;
        }

        if (vendor.Status != VendorStatus.Active)
        {
            operation.MarkFailed("Vendor is not active.");
            await context.SaveChangesAsync(cancellationToken);
            return;
        }

        var masterProductIds = operation.Items.Select(x => x.MasterProductId).Distinct().ToArray();
        var existingMasterProductIds = await context.MasterProducts
            .AsNoTracking()
            .Where(x => masterProductIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToHashSetAsync(cancellationToken);

        var existingVendorProductIds = await context.VendorProducts
            .AsNoTracking()
            .Where(x => x.VendorId == operation.VendorId && masterProductIds.Contains(x.MasterProductId))
            .Select(x => x.MasterProductId)
            .ToHashSetAsync(cancellationToken);

        var validBranchIds = await context.VendorBranches
            .AsNoTracking()
            .Where(x => x.VendorId == operation.VendorId)
            .Select(x => x.Id)
            .ToHashSetAsync(cancellationToken);

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
            else if (existingVendorProductIds.Contains(item.MasterProductId))
            {
                item.MarkSkipped("Product already exists in vendor store.");
            }
            else if (item.VendorBranchId.HasValue && !validBranchIds.Contains(item.VendorBranchId.Value))
            {
                item.MarkFailed("Branch is invalid for this vendor.");
            }
            else if (item.CompareAtPrice.HasValue && item.CompareAtPrice.Value <= item.SellingPrice)
            {
                item.MarkFailed("Compare price must be greater than selling price.");
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
                try
                {
                    var vendorProduct = new VendorProduct(
                        operation.VendorId,
                        item.MasterProductId,
                        item.SellingPrice,
                        item.StockQty,
                        item.CompareAtPrice,
                        item.VendorBranchId);

                    context.VendorProducts.Add(vendorProduct);
                    await context.SaveChangesAsync(cancellationToken);

                    item.MarkSucceeded(vendorProduct.Id);
                    existingVendorProductIds.Add(item.MasterProductId);
                }
                catch (DbUpdateException)
                {
                    item.MarkSkipped("Product already exists in vendor store.");
                }
                catch (Exception ex)
                {
                    item.MarkFailed(ex.Message);
                }
            }

            operation.RecalculateProgress();
            await context.SaveChangesAsync(cancellationToken);
        }

        operation.RecalculateProgress();
        await context.SaveChangesAsync(cancellationToken);
    }
}
