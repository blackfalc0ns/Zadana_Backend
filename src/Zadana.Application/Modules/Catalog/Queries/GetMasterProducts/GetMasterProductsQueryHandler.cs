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
            .Include(p => p.UnitOfMeasure)
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

        var projectedQuery = query
            .OrderByDescending(p => p.Id)
            .Select(p => new MasterProductDto(
                p.Id,
                p.NameAr,
                p.NameEn,
                p.Slug,
                p.DescriptionAr,
                p.DescriptionEn,
                p.Barcode,
                p.CategoryId,
                p.BrandId,
                p.Brand != null ? p.Brand.NameAr : null,
                p.Brand != null ? p.Brand.NameEn : null,
                p.UnitOfMeasureId,
                p.UnitOfMeasure != null ? p.UnitOfMeasure.NameAr : null,
                p.UnitOfMeasure != null ? p.UnitOfMeasure.NameEn : null,
                p.Status.ToString(),
                request.VendorId.HasValue && _context.VendorProducts.Any(vp => vp.MasterProductId == p.Id && vp.VendorId == request.VendorId.Value),
                p.Images.Select(i => new MasterProductImageDto(i.Url, i.AltText, i.DisplayOrder, i.IsPrimary)).ToList()
            ));

        return await PaginatedList<MasterProductDto>.CreateAsync(
            projectedQuery,
            request.PageNumber,
            request.PageSize);
    }
}
