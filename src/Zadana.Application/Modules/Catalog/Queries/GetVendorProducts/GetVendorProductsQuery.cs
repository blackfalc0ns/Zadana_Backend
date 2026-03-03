using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.DTOs;

namespace Zadana.Application.Modules.Catalog.Queries.GetVendorProducts;

public record GetVendorProductsQuery(Guid VendorId, Guid? CategoryId) : IRequest<List<VendorProductDto>>;

public class GetVendorProductsQueryHandler : IRequestHandler<GetVendorProductsQuery, List<VendorProductDto>>
{
    private readonly IApplicationDbContext _context;

    public GetVendorProductsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<VendorProductDto>> Handle(GetVendorProductsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.VendorProducts
            .AsNoTracking()
            .Include(vp => vp.MasterProduct)
            .Where(vp => vp.VendorId == request.VendorId);

        if (request.CategoryId.HasValue)
        {
            query = query.Where(vp => vp.MasterProduct.CategoryId == request.CategoryId.Value);
        }

        var products = await query.ToListAsync(cancellationToken);

        return products.Select(vp => new VendorProductDto(
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
                vp.MasterProduct.DescriptionAr,
                vp.MasterProduct.DescriptionEn,
                vp.MasterProduct.Barcode,
                vp.MasterProduct.CategoryId,
                vp.MasterProduct.BrandId,
                vp.MasterProduct.UnitOfMeasureId,
                vp.MasterProduct.Status.ToString()
            )
        )).ToList();
    }
}
