using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Caching;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Commands.VendorProducts.DeleteVendorProduct;

public class DeleteVendorProductCommandHandler : IRequestHandler<DeleteVendorProductCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheInvalidator _cacheInvalidator;

    public DeleteVendorProductCommandHandler(
        IApplicationDbContext context,
        ICacheInvalidator cacheInvalidator)
    {
        _context = context;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task Handle(DeleteVendorProductCommand request, CancellationToken cancellationToken)
    {
        var vendorProduct = await _context.VendorProducts
            .FirstOrDefaultAsync(vp => vp.Id == request.Id && vp.VendorId == request.VendorId, cancellationToken);

        if (vendorProduct == null)
        {
            throw new NotFoundException(nameof(VendorProduct), request.Id);
        }

        var hasOrderHistory = await _context.OrderItems
            .AnyAsync(item => item.VendorProductId == request.Id, cancellationToken);

        if (hasOrderHistory)
        {
            throw new BusinessRuleException(
                "VENDOR_PRODUCT_DELETE_BLOCKED_BY_ORDERS",
                "This product cannot be deleted because it is linked to existing orders.");
        }

        var hasFeaturedPlacement = await _context.FeaturedProductPlacements
            .AnyAsync(item => item.VendorProductId == request.Id, cancellationToken);

        if (hasFeaturedPlacement)
        {
            throw new BusinessRuleException(
                "VENDOR_PRODUCT_DELETE_BLOCKED_BY_MARKETING",
                "This product cannot be deleted because it is linked to active marketing placements.");
        }

        _context.VendorProducts.Remove(vendorProduct);
        await _context.SaveChangesAsync(cancellationToken);
        await _cacheInvalidator.RemoveByTagsAsync(CacheInvalidationProfiles.CatalogReadModels, cancellationToken);
    }
}
