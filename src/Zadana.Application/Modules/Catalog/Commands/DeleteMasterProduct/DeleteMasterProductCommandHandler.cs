using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Caching;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Catalog.Events;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Commands.DeleteMasterProduct;

public class DeleteMasterProductCommandHandler : IRequestHandler<DeleteMasterProductCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheInvalidator _cacheInvalidator;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly IPublisher _publisher;

    public DeleteMasterProductCommandHandler(
        IApplicationDbContext context,
        ICacheInvalidator cacheInvalidator,
        IStringLocalizer<SharedResource> localizer,
        IPublisher publisher)
    {
        _context = context;
        _cacheInvalidator = cacheInvalidator;
        _localizer = localizer;
        _publisher = publisher;
    }

    public async Task Handle(DeleteMasterProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _context.MasterProducts
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (product == null)
        {
            throw new NotFoundException("MasterProduct", request.Id);
        }

        // Guard: prevent deletion if vendor products exist
        var hasVendorProducts = await _context.VendorProducts
            .AnyAsync(vp => vp.MasterProductId == request.Id, cancellationToken);

        if (hasVendorProducts)
        {
            throw new BusinessRuleException(
                "PRODUCT_HAS_VENDOR_LISTINGS",
                "لا يمكن حذف المنتج لأنه مدرج عند تجار. أزل إدراجات التجار أولاً.|Cannot delete this product because it has vendor listings.");
        }

        // Guard: prevent deletion if order history exists
        var hasOrders = await _context.OrderItems
            .AnyAsync(oi => oi.MasterProductId == request.Id, cancellationToken);

        if (hasOrders)
        {
            throw new BusinessRuleException(
                "PRODUCT_HAS_ORDER_HISTORY",
                "لا يمكن حذف المنتج لأنه مرتبط بطلبات سابقة.|Cannot delete this product because it is linked to order history.");
        }

        // Guard: prevent deletion if in active carts
        var hasCartItems = await _context.CartItems
            .AnyAsync(ci => ci.MasterProductId == request.Id, cancellationToken);

        if (hasCartItems)
        {
            throw new BusinessRuleException(
                "PRODUCT_IN_ACTIVE_CARTS",
                "لا يمكن حذف المنتج لأنه موجود في سلات تسوق نشطة.|Cannot delete this product because it is in active shopping carts.");
        }

        // Soft-delete via DbContext SaveChangesAsync override (ISoftDeletable interception)
        _context.MasterProducts.Remove(product);
        await _context.SaveChangesAsync(cancellationToken);
        await _cacheInvalidator.RemoveByTagsAsync(CacheInvalidationProfiles.CatalogReadModels, cancellationToken);

        // Dispatch domain event to notify affected vendors
        await _publisher.Publish(
            new MasterProductDeletedEvent(
                product.Id,
                product.NameAr,
                product.NameEn,
                product.BrandId,
                product.CategoryId,
                product.DeletedAtUtc ?? DateTime.UtcNow),
            cancellationToken);
    }
}
