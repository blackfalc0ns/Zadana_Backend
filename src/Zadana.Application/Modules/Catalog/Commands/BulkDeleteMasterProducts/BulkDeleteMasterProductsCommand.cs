using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Caching;
using Zadana.Application.Common.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Commands.BulkDeleteMasterProducts;

public record BulkDeleteMasterProductsCommand(IReadOnlyList<Guid> Ids) : IRequest<BulkDeleteResult>;

public record BulkDeleteResult(int Deleted, int Skipped, IReadOnlyList<BulkDeleteSkippedItem> SkippedItems);
public record BulkDeleteSkippedItem(Guid Id, string Reason);

public class BulkDeleteMasterProductsCommandHandler : IRequestHandler<BulkDeleteMasterProductsCommand, BulkDeleteResult>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheInvalidator _cacheInvalidator;

    public BulkDeleteMasterProductsCommandHandler(IApplicationDbContext context, ICacheInvalidator cacheInvalidator)
    {
        _context = context;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task<BulkDeleteResult> Handle(BulkDeleteMasterProductsCommand request, CancellationToken cancellationToken)
    {
        if (request.Ids is null || request.Ids.Count == 0)
            throw new BadRequestException("IDS_REQUIRED", "At least one product ID is required.");

        if (request.Ids.Count > 100)
            throw new BadRequestException("TOO_MANY_IDS", "Cannot bulk delete more than 100 products at once.");

        var uniqueIds = request.Ids.Distinct().ToList();

        var products = await _context.MasterProducts
            .Where(p => uniqueIds.Contains(p.Id))
            .ToListAsync(cancellationToken);

        // Products that have vendor listings
        var idsWithVendorProducts = await _context.VendorProducts
            .Where(vp => uniqueIds.Contains(vp.MasterProductId))
            .Select(vp => vp.MasterProductId)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Products that have order history
        var idsWithOrders = await _context.OrderItems
            .Where(oi => uniqueIds.Contains(oi.MasterProductId))
            .Select(oi => oi.MasterProductId)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Products in active carts
        var idsInCarts = await _context.CartItems
            .Where(ci => uniqueIds.Contains(ci.MasterProductId))
            .Select(ci => ci.MasterProductId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var skipped = new List<BulkDeleteSkippedItem>();
        var toDelete = new List<Zadana.Domain.Modules.Catalog.Entities.MasterProduct>();

        foreach (var product in products)
        {
            if (idsWithVendorProducts.Contains(product.Id))
            {
                skipped.Add(new BulkDeleteSkippedItem(product.Id, "PRODUCT_HAS_VENDOR_LISTINGS"));
                continue;
            }
            if (idsWithOrders.Contains(product.Id))
            {
                skipped.Add(new BulkDeleteSkippedItem(product.Id, "PRODUCT_HAS_ORDER_HISTORY"));
                continue;
            }
            if (idsInCarts.Contains(product.Id))
            {
                skipped.Add(new BulkDeleteSkippedItem(product.Id, "PRODUCT_IN_ACTIVE_CARTS"));
                continue;
            }
            toDelete.Add(product);
        }

        // Add skipped entries for IDs not found in DB
        foreach (var id in uniqueIds.Where(id => products.All(p => p.Id != id)))
            skipped.Add(new BulkDeleteSkippedItem(id, "NOT_FOUND"));

        if (toDelete.Count > 0)
        {
            _context.MasterProducts.RemoveRange(toDelete);
            await _context.SaveChangesAsync(cancellationToken);
            await _cacheInvalidator.RemoveByTagsAsync(CacheInvalidationProfiles.CatalogReadModels, cancellationToken);
        }

        return new BulkDeleteResult(toDelete.Count, skipped.Count, skipped);
    }
}
