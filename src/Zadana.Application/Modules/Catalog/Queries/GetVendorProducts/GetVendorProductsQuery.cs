using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Models;
using Zadana.Application.Modules.Catalog.DTOs;

namespace Zadana.Application.Modules.Catalog.Queries.GetVendorProducts;

public record GetVendorProductsQuery(
    Guid VendorId, 
    Guid? CategoryId, 
    Guid? BranchId, 
    int PageNumber = 1, 
    int PageSize = 10) : IRequest<PaginatedList<VendorProductDto>>;

public class GetVendorProductsQueryHandler : IRequestHandler<GetVendorProductsQuery, PaginatedList<VendorProductDto>>
{
    private readonly IApplicationDbContext _context;

    public GetVendorProductsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<VendorProductDto>> Handle(GetVendorProductsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.VendorProducts
            .AsNoTracking()
            .Include(vp => vp.MasterProduct)
                .ThenInclude(mp => mp.Images)
            .Include(vp => vp.MasterProduct)
                .ThenInclude(mp => mp.Brand)
            .Include(vp => vp.MasterProduct)
                .ThenInclude(mp => mp.UnitOfMeasure)
            .Where(vp => vp.VendorId == request.VendorId);

        if (request.CategoryId.HasValue)
        {
            query = query.Where(vp => vp.MasterProduct.CategoryId == request.CategoryId.Value);
        }

        if (request.BranchId.HasValue)
        {
            query = query.Where(vp => vp.VendorBranchId == request.BranchId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        
        var products = await query
            .OrderBy(vp => vp.Id)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var items = products.Select(vp => new VendorProductDto(
            vp.Id,
            vp.VendorId,
            vp.MasterProductId,
            vp.SellingPrice,
            vp.CompareAtPrice,
            vp.StockQuantity,
            vp.IsAvailable,
            vp.Status.ToString(),
            new MasterProductDto(
                vp.MasterProduct.Id,
                vp.MasterProduct.NameAr,
                vp.MasterProduct.NameEn,
                vp.MasterProduct.Slug,
                vp.MasterProduct.DescriptionAr,
                vp.MasterProduct.DescriptionEn,
                vp.MasterProduct.Barcode,
                vp.MasterProduct.CategoryId,
                vp.MasterProduct.BrandId,
                vp.MasterProduct.Brand?.NameAr,
                vp.MasterProduct.Brand?.NameEn,
                vp.MasterProduct.UnitOfMeasureId,
                vp.MasterProduct.UnitOfMeasure?.NameAr,
                vp.MasterProduct.UnitOfMeasure?.NameEn,
                vp.MasterProduct.Status.ToString(),
                true,
                vp.MasterProduct.Images.Select(i => new MasterProductImageDto(i.Url, i.AltText, i.DisplayOrder, i.IsPrimary)).ToList(),
                vp.MasterProduct.CreatedAtUtc,
                vp.MasterProduct.UpdatedAtUtc
            )
        )).ToList();
        
        return new PaginatedList<VendorProductDto>(items, totalCount, request.PageNumber, request.PageSize);
    }
}
