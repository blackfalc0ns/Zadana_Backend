using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Caching;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.Commands.BulkDeleteMasterProducts;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Commands.BulkDeleteBrands;

public record BulkDeleteBrandsCommand(IReadOnlyList<Guid> Ids) : IRequest<BulkDeleteResult>;

public class BulkDeleteBrandsCommandHandler : IRequestHandler<BulkDeleteBrandsCommand, BulkDeleteResult>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheInvalidator _cacheInvalidator;

    public BulkDeleteBrandsCommandHandler(IApplicationDbContext context, ICacheInvalidator cacheInvalidator)
    {
        _context = context;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task<BulkDeleteResult> Handle(BulkDeleteBrandsCommand request, CancellationToken cancellationToken)
    {
        if (request.Ids is null || request.Ids.Count == 0)
            throw new BadRequestException("IDS_REQUIRED", "At least one brand ID is required.");

        if (request.Ids.Count > 50)
            throw new BadRequestException("TOO_MANY_IDS", "Cannot bulk delete more than 50 brands at once.");

        var uniqueIds = request.Ids.Distinct().ToList();

        var brands = await _context.Brands
            .Where(b => uniqueIds.Contains(b.Id))
            .ToListAsync(cancellationToken);

        // Brands with linked master products
        var idsWithProducts = await _context.MasterProducts
            .Where(p => p.BrandId.HasValue && uniqueIds.Contains(p.BrandId!.Value))
            .Select(p => p.BrandId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var skipped = new List<BulkDeleteSkippedItem>();
        var toDelete = new List<Zadana.Domain.Modules.Catalog.Entities.Brand>();

        foreach (var brand in brands)
        {
            if (idsWithProducts.Contains(brand.Id))
            {
                skipped.Add(new BulkDeleteSkippedItem(brand.Id, "BRAND_HAS_PRODUCTS"));
                continue;
            }
            toDelete.Add(brand);
        }

        // IDs not found in DB
        foreach (var id in uniqueIds.Where(id => brands.All(b => b.Id != id)))
            skipped.Add(new BulkDeleteSkippedItem(id, "NOT_FOUND"));

        if (toDelete.Count > 0)
        {
            // Nullify brand references in ProductRequests before deleting
            var deletingIds = toDelete.Select(b => b.Id).ToList();

            await _context.BrandRequests
                .Where(r => r.CreatedBrandId.HasValue && deletingIds.Contains(r.CreatedBrandId.Value))
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.CreatedBrandId, (Guid?)null), cancellationToken);

            await _context.ProductRequests
                .Where(r => r.SuggestedBrandId.HasValue && deletingIds.Contains(r.SuggestedBrandId.Value))
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.SuggestedBrandId, (Guid?)null), cancellationToken);

            _context.Brands.RemoveRange(toDelete);
            await _context.SaveChangesAsync(cancellationToken);
            await _cacheInvalidator.RemoveByTagsAsync(CacheInvalidationProfiles.CatalogReadModels, cancellationToken);
        }

        return new BulkDeleteResult(toDelete.Count, skipped.Count, skipped);
    }
}
