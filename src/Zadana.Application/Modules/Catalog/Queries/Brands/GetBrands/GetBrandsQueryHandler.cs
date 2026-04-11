using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.DTOs;

namespace Zadana.Application.Modules.Catalog.Queries.Brands.GetBrands;

public class GetBrandsQueryHandler : IRequestHandler<GetBrandsQuery, List<BrandDto>>
{
    private readonly IApplicationDbContext _context;

    public GetBrandsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<BrandDto>> Handle(GetBrandsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Brands.AsNoTracking();

        if (!request.IncludeInactive)
        {
            query = query.Where(b => b.IsActive);
        }

        var brands = await query
            .OrderBy(b => b.NameEn)
            .Select(b => new BrandDto(
                b.Id,
                b.NameAr,
                b.NameEn,
                b.LogoUrl,
                b.CategoryId,
                b.Category != null ? b.Category.NameAr : null,
                b.Category != null ? b.Category.NameEn : null,
                b.IsActive,
                b.MasterProducts.Count(),
                b.CreatedAtUtc,
                b.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return brands;
    }
}
