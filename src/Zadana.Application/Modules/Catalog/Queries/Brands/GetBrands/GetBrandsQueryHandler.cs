using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Domain.Modules.Catalog.Entities;

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
            .Include(b => b.Category)
            .Include(b => b.BrandCategories)
                .ThenInclude(link => link.Category)
            .Include(b => b.MasterProducts)
            .OrderBy(b => b.NameEn)
            .ToListAsync(cancellationToken);

        return brands.Select(MapBrand).ToList();
    }

    private static BrandDto MapBrand(Brand brand)
    {
        var categories = brand.BrandCategories
            .Where(link => link.Category is not null)
            .OrderBy(link => link.Category.NameEn)
            .Select(link => new BrandCategoryLinkDto(link.CategoryId, link.Category.NameAr, link.Category.NameEn))
            .ToList();

        if (categories.Count == 0 && brand.CategoryId.HasValue)
        {
            categories.Add(new BrandCategoryLinkDto(
                brand.CategoryId.Value,
                brand.Category?.NameAr,
                brand.Category?.NameEn));
        }

        return new BrandDto(
            brand.Id,
            brand.NameAr,
            brand.NameEn,
            brand.LogoUrl,
            brand.CoverImageUrl,
            brand.CategoryId,
            categories.Select(item => item.CategoryId).ToList(),
            categories,
            brand.Category?.NameAr,
            brand.Category?.NameEn,
            brand.IsActive,
            brand.MasterProducts.Count,
            brand.CreatedAtUtc,
            brand.UpdatedAtUtc);
    }
}
