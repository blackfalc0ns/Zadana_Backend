using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Models;
using Zadana.Application.Modules.Catalog.DTOs;

namespace Zadana.Application.Modules.Catalog.Queries.GetProductVendors;

public record GetProductVendorsQuery(Guid ProductId, int PageNumber = 1, int PageSize = 10) : IRequest<PaginatedList<ProductVendorSnapshotDto>>;

public class GetProductVendorsQueryHandler : IRequestHandler<GetProductVendorsQuery, PaginatedList<ProductVendorSnapshotDto>>
{
    private readonly IApplicationDbContext _context;

    public GetProductVendorsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<ProductVendorSnapshotDto>> Handle(GetProductVendorsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.VendorProducts
            .AsNoTracking()
            .Include(vp => vp.Vendor)
            .Where(vp => vp.MasterProductId == request.ProductId);

        var totalCount = await query.CountAsync(cancellationToken);
        
        var vendorProducts = await query
            .OrderByDescending(vp => vp.UpdatedAtUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var items = vendorProducts.Select(vp => new ProductVendorSnapshotDto(
            vp.VendorId,
            vp.Vendor.BusinessNameAr,
            vp.Vendor.BusinessNameEn ?? vp.Vendor.BusinessNameAr,
            vp.StockQuantity,
            vp.SellingPrice,
            vp.UpdatedAtUtc
        )).ToList();
        
        return new PaginatedList<ProductVendorSnapshotDto>(items, totalCount, request.PageNumber, request.PageSize);
    }
}
