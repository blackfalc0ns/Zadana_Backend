using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Queries.VendorProducts.GetVendorProductById;

public record GetVendorProductByIdQuery(Guid VendorId, Guid ProductId) : IRequest<VendorProductDto>;

public class GetVendorProductByIdQueryHandler : IRequestHandler<GetVendorProductByIdQuery, VendorProductDto>
{
    private readonly IApplicationDbContext _context;

    public GetVendorProductByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<VendorProductDto> Handle(GetVendorProductByIdQuery request, CancellationToken cancellationToken)
    {
        var vp = await _context.VendorProducts
            .AsNoTracking()
            .Include(x => x.MasterProduct)
                .ThenInclude(mp => mp.Images)
            .FirstOrDefaultAsync(x => x.Id == request.ProductId && x.VendorId == request.VendorId, cancellationToken);

        if (vp == null)
            throw new NotFoundException("VendorProduct", request.ProductId);

        return new VendorProductDto(
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
                vp.MasterProduct.UnitOfMeasureId,
                vp.MasterProduct.Status.ToString(),
                vp.MasterProduct.Images.Select(i => new MasterProductImageDto(i.Url, i.AltText, i.DisplayOrder, i.IsPrimary)).ToList()
            )
        );
    }
}
