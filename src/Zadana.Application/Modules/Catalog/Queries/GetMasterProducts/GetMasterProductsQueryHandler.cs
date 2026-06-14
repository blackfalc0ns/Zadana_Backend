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

        var items = products
            .Select(p => MasterProductDisplayDto.ToDto(
                p,
                inVendorStoreProductIds.Contains(p.Id)))
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
}
