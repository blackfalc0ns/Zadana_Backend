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
            .FirstOrDefaultAsync(vp =>
                vp.Id == request.Id &&
                vp.VendorId == request.VendorId &&
                (!request.BranchId.HasValue || vp.VendorBranchId == request.BranchId.Value),
                cancellationToken);

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
                "ما قدرنا نحذف المنتج الآن. تأكد أنه غير مرتبط بطلبات أو حملات.|Unable to delete this product right now. Make sure it is not linked to orders or campaigns.");
        }

        var hasFeaturedPlacement = await _context.FeaturedProductPlacements
            .AnyAsync(item => item.VendorProductId == request.Id, cancellationToken);

        if (hasFeaturedPlacement)
        {
            throw new BusinessRuleException(
                "VENDOR_PRODUCT_DELETE_BLOCKED_BY_MARKETING",
                "ما قدرنا نحذف المنتج الآن. تأكد أنه غير مرتبط بطلبات أو حملات.|Unable to delete this product right now. Make sure it is not linked to orders or campaigns.");
        }

        _context.VendorProducts.Remove(vendorProduct);
        await _context.SaveChangesAsync(cancellationToken);
        await _cacheInvalidator.RemoveByTagsAsync(CacheInvalidationProfiles.CatalogReadModels, cancellationToken);
    }
}
