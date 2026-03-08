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
        var query = _context.MasterProducts.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            query = query.Where(p => p.NameAr.Contains(request.SearchTerm) || p.NameEn.Contains(request.SearchTerm));
        }

        if (request.CategoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == request.CategoryId.Value);
        }

        var projectedQuery = query
            .OrderByDescending(p => p.Id)
            .Select(p => new MasterProductDto(
                p.Id,
                p.NameAr,
                p.NameEn,
                p.DescriptionAr,
                p.DescriptionEn,
                p.Barcode,
                p.CategoryId,
                p.BrandId,
                p.UnitOfMeasureId,
                p.Status.ToString(),
                p.Images.Select(i => new MasterProductImageDto(i.Url, i.AltText, i.DisplayOrder, i.IsPrimary)).ToList()
            ));

        return await PaginatedList<MasterProductDto>.CreateAsync(
            projectedQuery,
            request.PageNumber,
            request.PageSize);
    }
}
