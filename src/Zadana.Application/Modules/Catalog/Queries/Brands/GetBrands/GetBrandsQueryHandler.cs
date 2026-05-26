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

        // Safety cap: use SearchBrands for paginated large result sets
        var brands = await query
            .Include(b => b.Category)
            .Include(b => b.BrandCategories)
                .ThenInclude(link => link.Category)
            .OrderBy(b => b.NameEn)
            .Take(1000)
            .ToListAsync(cancellationToken);

        // Efficient count via grouped query instead of loading all MasterProducts
        var brandIds = brands.Select(b => b.Id).ToList();
        var productCountsByBrand = await _context.MasterProducts
            .AsNoTracking()
            .Where(p => p.BrandId.HasValue && brandIds.Contains(p.BrandId!.Value))
            .GroupBy(p => p.BrandId!.Value)
            .Select(g => new { BrandId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.BrandId, x => x.Count, cancellationToken);

        return brands.Select(b => MapBrand(b, productCountsByBrand)).ToList();
    }

    private static BrandDto MapBrand(Brand brand, IReadOnlyDictionary<Guid, int> productCounts)
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

        productCounts.TryGetValue(brand.Id, out var masterProductCount);

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
            masterProductCount,
            brand.CreatedAtUtc,
            brand.UpdatedAtUtc);
    }
}
