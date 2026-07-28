using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Caching;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Catalog.Common;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Commands.VendorProducts.UpdateVendorProduct;

public class UpdateVendorProductCommandHandler : IRequestHandler<UpdateVendorProductCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheInvalidator _cacheInvalidator;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public UpdateVendorProductCommandHandler(
        IApplicationDbContext context,
        ICacheInvalidator cacheInvalidator,
        IStringLocalizer<SharedResource> localizer)
    {
        _context = context;
        _cacheInvalidator = cacheInvalidator;
        _localizer = localizer;
    }

    public async Task Handle(UpdateVendorProductCommand request, CancellationToken cancellationToken)
    {
        var vendorProduct = await _context.VendorProducts
            .Include(vp => vp.VendorBranch)
            .FirstOrDefaultAsync(vp =>
                vp.Id == request.Id &&
                vp.VendorId == request.VendorId &&
                (!request.BranchId.HasValue || vp.VendorBranchId == request.BranchId.Value),
                cancellationToken);

        if (vendorProduct == null)
            throw new NotFoundException(nameof(VendorProduct), request.Id);

        if (!request.TradePrice.HasValue)
            throw new BusinessRuleException("TRADE_PRICE_REQUIRED", _localizer["TRADE_PRICE_REQUIRED"]);

        var productRows = await _context.VendorProducts
            .Where(vp =>
                vp.VendorId == request.VendorId &&
                vp.MasterProductId == vendorProduct.MasterProductId)
            .ToListAsync(cancellationToken);

        var originBranchId = productRows
            .OrderBy(row => row.CreatedAtUtc)
            .Select(row => row.VendorBranchId)
            .FirstOrDefault();

        var canUpdateCanonicalPricing = VendorProductPricingAuthority.CanEditPrice(
            vendorProduct.VendorBranchId,
            vendorProduct.VendorBranch?.IsPrimary == true,
            originBranchId);

        var priceChanged = VendorProductPricingAuthority.PricesDiffer(
            vendorProduct.SellingPrice,
            vendorProduct.CompareAtPrice,
            vendorProduct.CostPrice,
            vendorProduct.TradePrice,
            request.SellingPrice,
            request.CompareAtPrice,
            request.CostPrice,
            request.TradePrice);

        if (priceChanged && !canUpdateCanonicalPricing)
        {
            throw new BusinessRuleException(
                "VENDOR_PRODUCT_PRICE_LOCKED",
                _localizer["VENDOR_PRODUCT_PRICE_LOCKED"]);
        }

        if (canUpdateCanonicalPricing)
        {
            foreach (var productRow in productRows)
            {
                productRow.UpdatePricing(request.SellingPrice, request.CompareAtPrice, request.CostPrice, request.TradePrice);
            }
        }

        foreach (var productRow in productRows)
        {
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
