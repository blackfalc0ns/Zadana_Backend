using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Caching;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Commands.CreateVendorProduct;

public class CreateVendorProductCommandHandler : IRequestHandler<CreateVendorProductCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheInvalidator _cacheInvalidator;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public CreateVendorProductCommandHandler(
        IApplicationDbContext context,
        ICacheInvalidator cacheInvalidator,
        IStringLocalizer<SharedResource> localizer)
    {
        _context = context;
        _cacheInvalidator = cacheInvalidator;
        _localizer = localizer;
    }

    public async Task<Guid> Handle(CreateVendorProductCommand request, CancellationToken cancellationToken)
    {
        var vendor = await _context.Vendors
            .FirstOrDefaultAsync(v => v.Id == request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);

        if (vendor.Status != VendorStatus.Active)
        {
            throw new BusinessRuleException("VENDOR_NOT_VERIFIED", _localizer["VENDOR_NOT_VERIFIED"]);
        }

        var masterProductExists = _context.MasterProducts.Any(mp => mp.Id == request.MasterProductId);
        if (!masterProductExists)
        {
            throw new NotFoundException("MasterProduct", request.MasterProductId);
        }

        if (!request.TradePrice.HasValue)
        {
            throw new BusinessRuleException("TRADE_PRICE_REQUIRED", "Trade price is required.");
        }

        if (request.BranchId.HasValue)
        {
            var branchBelongsToVendor = await _context.VendorBranches
                .AnyAsync(branch => branch.Id == request.BranchId.Value && branch.VendorId == request.VendorId, cancellationToken);

            if (!branchBelongsToVendor)
            {
                throw new NotFoundException("VendorBranch", request.BranchId.Value);
            }
        }

        var productExistsInScope = await _context.VendorProducts
            .AsNoTracking()
            .AnyAsync(product =>
                product.VendorId == request.VendorId &&
                product.MasterProductId == request.MasterProductId &&
                product.VendorBranchId == request.BranchId,
                cancellationToken);

        if (productExistsInScope)
        {
            throw new BusinessRuleException("VENDOR_PRODUCT_ALREADY_EXISTS", "Product already exists in this vendor branch.");
        }

        var canonicalPricing = await _context.VendorProducts
            .AsNoTracking()
            .Where(product =>
                product.VendorId == request.VendorId &&
                product.MasterProductId == request.MasterProductId)
            .OrderBy(product => product.VendorBranchId.HasValue)
            .ThenBy(product => product.CreatedAtUtc)
            .Select(product => new
            {
                product.SellingPrice,
                product.CompareAtPrice,
                product.CostPrice,
                product.TradePrice
            })
            .FirstOrDefaultAsync(cancellationToken);

        var vendorProduct = new VendorProduct(
            vendorId: request.VendorId,
            masterProductId: request.MasterProductId,
            sellingPrice: canonicalPricing?.SellingPrice ?? request.SellingPrice,
            stockQuantity: request.StockQty,
            compareAtPrice: canonicalPricing?.CompareAtPrice ?? request.CompareAtPrice,
            costPrice: canonicalPricing?.CostPrice ?? request.CostPrice,
            tradePrice: canonicalPricing?.TradePrice ?? request.TradePrice,
            vendorBranchId: request.BranchId
        );

        _context.VendorProducts.Add(vendorProduct);
        await _context.SaveChangesAsync(cancellationToken);
        await _cacheInvalidator.RemoveByTagsAsync(CacheInvalidationProfiles.CatalogReadModels, cancellationToken);

        return vendorProduct.Id;
    }
}
