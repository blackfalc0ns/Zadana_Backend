using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Models;
using Zadana.Application.Modules.Catalog.DTOs;

namespace Zadana.Application.Modules.Catalog.Queries.GetMasterProducts;

public class GetMasterProductsQueryHandler : IRequestHandler<GetMasterProductsQuery, PaginatedList<MasterProductDto>>
{
    private readonly IApplicationDbContext _context;

    public GetMasterProductsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<MasterProductDto>> Handle(GetMasterProductsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.MasterProducts
            .Include(p => p.Images)
            .Include(p => p.Brand)
            .Include(p => p.PackageType)
            .Include(p => p.MeasurementUnit)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            query = query.Where(p => p.NameAr.Contains(request.SearchTerm) || p.NameEn.Contains(request.SearchTerm));
        }

        if (request.CategoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == request.CategoryId.Value);
        }

        if (request.BrandId.HasValue)
        {
            query = query.Where(p => p.BrandId == request.BrandId.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(p => p.Status == request.Status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var products = await query
            .OrderByDescending(p => p.Id)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var productIds = products.Select(product => product.Id).ToArray();
        var inVendorStoreProductIds = request.VendorId.HasValue && productIds.Length > 0
            ? await BuildVendorStoreProductSetAsync(request.VendorId.Value, request.VendorBranchId, productIds, cancellationToken)
            : new HashSet<Guid>();
        var vendorPricingByProductId = request.VendorId.HasValue && productIds.Length > 0
            ? await BuildVendorPricingMapAsync(request.VendorId.Value, productIds, cancellationToken)
            : new Dictionary<Guid, VendorProductPricingSnapshot>();

        var items = products
            .Select(p =>
            {
                vendorPricingByProductId.TryGetValue(p.Id, out var pricing);
                return MasterProductDisplayDto.ToDto(
                    p,
                    inVendorStoreProductIds.Contains(p.Id),
                    vendorSellingPrice: pricing?.SellingPrice,
                    vendorCompareAtPrice: pricing?.CompareAtPrice,
                    vendorCostPrice: pricing?.CostPrice,
                    vendorTradePrice: pricing?.TradePrice);
            })
            .ToList();

        return new PaginatedList<MasterProductDto>(items, totalCount, request.PageNumber, request.PageSize);
    }

    private async Task<HashSet<Guid>> BuildVendorStoreProductSetAsync(
        Guid vendorId,
        Guid? vendorBranchId,
        Guid[] productIds,
        CancellationToken cancellationToken)
    {
        var query = _context.VendorProducts
            .AsNoTracking()
            .Where(product =>
                product.VendorId == vendorId &&
                productIds.Contains(product.MasterProductId));

        if (vendorBranchId.HasValue)
        {
            query = query.Where(product => product.VendorBranchId == vendorBranchId.Value);
        }

        return await query
            .Select(product => product.MasterProductId)
            .Distinct()
            .ToHashSetAsync(cancellationToken);
    }

    private async Task<Dictionary<Guid, VendorProductPricingSnapshot>> BuildVendorPricingMapAsync(
        Guid vendorId,
        Guid[] productIds,
        CancellationToken cancellationToken)
    {
        var rows = await _context.VendorProducts
            .AsNoTracking()
            .Where(product =>
                product.VendorId == vendorId &&
                productIds.Contains(product.MasterProductId))
            .Select(product => new
            {
                product.MasterProductId,
                product.SellingPrice,
                product.CompareAtPrice,
                product.CostPrice,
                product.TradePrice,
                IsPrimaryBranch = product.VendorBranch != null && product.VendorBranch.IsPrimary,
                IsStoreWide = product.VendorBranchId == null,
                product.UpdatedAtUtc,
                product.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(product => product.MasterProductId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var product = group
                        .OrderByDescending(item => item.IsPrimaryBranch)
                        .ThenByDescending(item => item.IsStoreWide)
                        .ThenByDescending(item => item.UpdatedAtUtc)
                        .ThenByDescending(item => item.CreatedAtUtc)
                        .First();

                    return new VendorProductPricingSnapshot(
                        product.SellingPrice,
                        product.CompareAtPrice,
                        product.CostPrice,
                        product.TradePrice);
                });
    }

    private sealed record VendorProductPricingSnapshot(
        decimal SellingPrice,
        decimal? CompareAtPrice,
        decimal? CostPrice,
        decimal? TradePrice);
}
