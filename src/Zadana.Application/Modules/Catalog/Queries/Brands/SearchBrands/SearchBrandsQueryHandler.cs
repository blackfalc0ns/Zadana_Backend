using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Domain.Modules.Catalog.Entities;

namespace Zadana.Application.Modules.Catalog.Queries.Brands.SearchBrands;

public class SearchBrandsQueryHandler : IRequestHandler<SearchBrandsQuery, CatalogSearchResponse<BrandDto, BrandSearchFiltersDto, BrandSearchFacetsDto>>
{
    private readonly IApplicationDbContext _context;

    public SearchBrandsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CatalogSearchResponse<BrandDto, BrandSearchFiltersDto, BrandSearchFacetsDto>> Handle(SearchBrandsQuery request, CancellationToken cancellationToken)
    {
        var filters = request.Filters ?? new BrandSearchFiltersDto();
        var query = _context.Brands
            .AsNoTracking()
            .Include(brand => brand.MasterProducts)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(brand =>
                brand.NameAr.Contains(search) ||
                brand.NameEn.Contains(search));
        }

        if (filters.IsActive.HasValue)
        {
            query = query.Where(brand => brand.IsActive == filters.IsActive.Value);
        }

        if (filters.CategoryId.HasValue)
        {
            query = query.Where(brand => brand.CategoryId == filters.CategoryId.Value);
        }

        if (filters.HasProducts.HasValue)
        {
            query = filters.HasProducts.Value
                ? query.Where(brand => brand.MasterProducts.Any())
                : query.Where(brand => !brand.MasterProducts.Any());
        }

        if (filters.CreatedAtFrom.HasValue)
        {
            query = query.Where(brand => brand.CreatedAtUtc >= filters.CreatedAtFrom.Value);
        }

        if (filters.CreatedAtTo.HasValue)
        {
            var createdTo = ToInclusiveUpperBound(filters.CreatedAtTo.Value);
            query = query.Where(brand => brand.CreatedAtUtc <= createdTo);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));

        var items = await ApplySorting(query, request.SortField, request.SortDirection)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(brand => new BrandDto(
                brand.Id,
                brand.NameAr,
                brand.NameEn,
                brand.LogoUrl,
                brand.CategoryId,
                brand.Category != null ? brand.Category.NameAr : null,
                brand.Category != null ? brand.Category.NameEn : null,
                brand.IsActive,
                brand.MasterProducts.Count,
                brand.CreatedAtUtc,
                brand.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        var activeCount = await query.CountAsync(brand => brand.IsActive, cancellationToken);
        var withProductsCount = await query.CountAsync(brand => brand.MasterProducts.Any(), cancellationToken);
        var facets = new BrandSearchFacetsDto(
            activeCount,
            totalCount - activeCount,
            withProductsCount);

        return new CatalogSearchResponse<BrandDto, BrandSearchFiltersDto, BrandSearchFacetsDto>(
            items,
            totalCount,
            totalPages,
            pageNumber,
            pageSize,
            filters,
            [
                new("nameEn", "asc", "English name"),
                new("nameAr", "asc", "Arabic name"),
                new("createdAtUtc", "desc", "Newest created"),
                new("masterProductsCount", "desc", "Most products")
            ],
            facets);
    }

    private static IQueryable<Brand> ApplySorting(IQueryable<Brand> query, string? sortField, string? sortDirection)
    {
        var normalizedField = sortField?.Trim().ToLowerInvariant();
        var descending = !string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        return normalizedField switch
        {
            "namear" => descending ? query.OrderByDescending(brand => brand.NameAr) : query.OrderBy(brand => brand.NameAr),
            "createdatutc" or "createdat" => descending ? query.OrderByDescending(brand => brand.CreatedAtUtc) : query.OrderBy(brand => brand.CreatedAtUtc),
            "masterproductscount" => descending ? query.OrderByDescending(brand => brand.MasterProducts.Count) : query.OrderBy(brand => brand.MasterProducts.Count),
            _ => descending ? query.OrderByDescending(brand => brand.NameEn) : query.OrderBy(brand => brand.NameEn)
        };
    }

    private static DateTime ToInclusiveUpperBound(DateTime value)
    {
        return value.TimeOfDay == TimeSpan.Zero
            ? value.Date.AddDays(1).AddTicks(-1)
            : value;
    }
}
