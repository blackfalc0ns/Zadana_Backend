using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Caching;
using Zadana.Application.Common.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Commands.SetMasterProductCardPriceVisibility;

public class SetMasterProductCardPriceVisibilityCommandHandler
    : IRequestHandler<SetMasterProductCardPriceVisibilityCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheInvalidator _cacheInvalidator;

    public SetMasterProductCardPriceVisibilityCommandHandler(
        IApplicationDbContext context,
        ICacheInvalidator cacheInvalidator)
    {
        _context = context;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task<Unit> Handle(
        SetMasterProductCardPriceVisibilityCommand request,
        CancellationToken cancellationToken)
    {
        var product = await _context.MasterProducts
            .FirstOrDefaultAsync(item => item.Id == request.ProductId, cancellationToken);

        if (product is null)
        {
            throw new NotFoundException("MasterProduct", request.ProductId);
        }

        var variantGroupId = product.VariantGroupId == Guid.Empty
            ? product.Id
            : product.VariantGroupId;

        var products = await _context.MasterProducts
            .Where(item => item.Id == product.Id || item.VariantGroupId == variantGroupId)
            .ToListAsync(cancellationToken);

        foreach (var item in products)
        {
            item.SetCardPriceVisibility(request.ShowPriceOnCard);
        }

        await _context.SaveChangesAsync(cancellationToken);
        await _cacheInvalidator.RemoveByTagsAsync(CacheInvalidationProfiles.CatalogReadModels, cancellationToken);

        return Unit.Value;
    }
}
