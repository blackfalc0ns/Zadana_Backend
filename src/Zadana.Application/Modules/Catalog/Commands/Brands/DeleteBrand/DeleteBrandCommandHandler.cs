using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Caching;
using Zadana.Application.Common.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Commands.Brands.DeleteBrand;

public class DeleteBrandCommandHandler : IRequestHandler<DeleteBrandCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheInvalidator _cacheInvalidator;

    public DeleteBrandCommandHandler(IApplicationDbContext context, ICacheInvalidator cacheInvalidator)
    {
        _context = context;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task Handle(DeleteBrandCommand request, CancellationToken cancellationToken)
    {
        var brand = await _context.Brands
            .FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken);

        if (brand is null)
        {
            throw new NotFoundException("Brand", request.Id);
        }

        var hasProducts = await _context.MasterProducts
            .AnyAsync(product => product.BrandId == request.Id, cancellationToken);

        if (hasProducts)
        {
            throw new BusinessRuleException(
                "BRAND_HAS_PRODUCTS",
                "Cannot delete brand with products. Please remove or reassign products first.");
        }

        await _context.BrandRequests
            .Where(requestEntity => requestEntity.CreatedBrandId == request.Id)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(requestEntity => requestEntity.CreatedBrandId, (Guid?)null),
                cancellationToken);

        await _context.ProductRequests
            .Where(requestEntity => requestEntity.SuggestedBrandId == request.Id)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(requestEntity => requestEntity.SuggestedBrandId, (Guid?)null),
                cancellationToken);

        _context.Brands.Remove(brand);
        await _context.SaveChangesAsync(cancellationToken);
        await _cacheInvalidator.RemoveByTagsAsync(CacheInvalidationProfiles.CatalogReadModels, cancellationToken);
    }
}
