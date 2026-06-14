using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Caching;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Commands.VendorProducts.UpdateVendorProduct;

public class UpdateVendorProductCommandHandler : IRequestHandler<UpdateVendorProductCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheInvalidator _cacheInvalidator;

    public UpdateVendorProductCommandHandler(IApplicationDbContext context, ICacheInvalidator cacheInvalidator)
    {
        _context = context;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task Handle(UpdateVendorProductCommand request, CancellationToken cancellationToken)
    {
        var vendorProduct = await _context.VendorProducts
            .FirstOrDefaultAsync(vp =>
                vp.Id == request.Id &&
                vp.VendorId == request.VendorId &&
                (!request.BranchId.HasValue || vp.VendorBranchId == request.BranchId.Value),
                cancellationToken);

        if (vendorProduct == null)
            throw new NotFoundException(nameof(VendorProduct), request.Id);

        if (!request.TradePrice.HasValue)
            throw new BusinessRuleException("TRADE_PRICE_REQUIRED", "Trade price is required.");

        var productRows = await _context.VendorProducts
            .Where(vp =>
                vp.VendorId == request.VendorId &&
                vp.MasterProductId == vendorProduct.MasterProductId)
            .ToListAsync(cancellationToken);

        foreach (var productRow in productRows)
        {
            productRow.UpdatePricing(request.SellingPrice, request.CompareAtPrice, request.CostPrice, request.TradePrice);
            productRow.UpdateCustomDetails(
                request.CustomNameAr,
                request.CustomNameEn,
                request.CustomDescriptionAr,
                request.CustomDescriptionEn);
        }

        vendorProduct.UpdateStock(request.StockQty);

        await _context.SaveChangesAsync(cancellationToken);
        await _cacheInvalidator.RemoveByTagsAsync(CacheInvalidationProfiles.CatalogReadModels, cancellationToken);
    }
}
