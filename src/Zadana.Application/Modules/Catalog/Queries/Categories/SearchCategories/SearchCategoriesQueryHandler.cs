using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Domain.Modules.Catalog.Entities;

namespace Zadana.Application.Modules.Catalog.Queries.Categories.SearchCategories;

public class SearchCategoriesQueryHandler : IRequestHandler<SearchCategoriesQuery, CatalogSearchResponse<CategoryDto, CategorySearchFiltersDto, CategorySearchFacetsDto>>
{
    private readonly IApplicationDbContext _context;

    public SearchCategoriesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CatalogSearchResponse<CategoryDto, CategorySearchFiltersDto, CategorySearchFacetsDto>> Handle(SearchCategoriesQuery request, CancellationToken cancellationToken)
    {
        var filters = request.Filters ?? new CategorySearchFiltersDto();
        var categories = await _context.Categories
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var masterProductCounts = await _context.MasterProducts
            .AsNoTracking()
            .GroupBy(product => product.CategoryId)
            .Select(group => new { CategoryId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.CategoryId, item => item.Count, cancellationToken);
        var brandsCountByCategoryId = await _context.Brands
            .AsNoTracking()
            .Where(brand => brand.CategoryId.HasValue)
            .GroupBy(brand => brand.CategoryId!.Value)
            .Select(group => new { CategoryId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.CategoryId, item => item.Count, cancellationToken);

        var projections = categories.Select(category =>
        {
            var level = ResolveLevel(category, categories);
            var hasChildren = categories.Any(item => item.ParentCategoryId == category.Id);
            var parent = categories.FirstOrDefault(item => item.Id == category.ParentCategoryId);
            var masterProductsCount = masterProductCounts.TryGetValue(category.Id, out var count) ? count : 0;
            var brandsCount = brandsCountByCategoryId.TryGetValue(category.Id, out var brandCount) ? brandCount : 0;

            return new SearchCategoryProjection(
                category,
                level,
                hasChildren,
                parent?.NameAr,
                parent?.NameEn,
                masterProductsCount,
                brandsCount);
        });

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            projections = projections.Where(item =>
                item.Entity.NameAr.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                item.Entity.NameEn.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (filters.ParentCategoryId.HasValue)
        {
            projections = projections.Where(item => item.Entity.ParentCategoryId == filters.ParentCategoryId.Value);
        }

        if (filters.Level.HasValue)
        {
            projections = projections.Where(item => item.Level == filters.Level.Value);
        }

        if (filters.IsActive.HasValue)
        {
            projections = projections.Where(item => item.Entity.IsActive == filters.IsActive.Value);
        }

        if (filters.HasChildren.HasValue)
        {
            projections = projections.Where(item => item.HasChildren == filters.HasChildren.Value);
        }

        if (filters.CreatedAtFrom.HasValue)
        {
            projections = projections.Where(item => item.Entity.CreatedAtUtc >= filters.CreatedAtFrom.Value);
        }

        if (filters.CreatedAtTo.HasValue)
        {
            var createdTo = ToInclusiveUpperBound(filters.CreatedAtTo.Value);
            projections = projections.Where(item => item.Entity.CreatedAtUtc <= createdTo);
        }

        var filtered = projections.ToList();
        var totalCount = filtered.Count;
        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        var items = ApplySorting(filtered, request.SortField, request.SortDirection)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new CategoryDto(
                item.Entity.Id,
                item.Entity.NameAr,
                item.Entity.NameEn,
                item.Entity.ImageUrl,
                item.Entity.ParentCategoryId,
                item.Entity.DisplayOrder,
                item.Entity.IsActive,
                item.ParentNameAr,
                item.ParentNameEn,
                item.Entity.CreatedAtUtc,
                item.Entity.UpdatedAtUtc,
                item.MasterProductsCount,
                item.BrandsCount,
                item.Level,
                null))
            .ToList();

        var facets = new CategorySearchFacetsDto(
            filtered.GroupBy(item => item.Level)
                .OrderBy(group => group.Key)
                .Select(group => new CatalogFacetCountDto(group.Key.ToString(), $"المستوى {group.Key}", $"Level {group.Key}", group.Count()))
                .ToList(),
            filtered.Count(item => item.Entity.IsActive),
            filtered.Count(item => !item.Entity.IsActive),
            filtered.Count(item => item.HasChildren));

        return new CatalogSearchResponse<CategoryDto, CategorySearchFiltersDto, CategorySearchFacetsDto>(
            items,
            totalCount,
            totalPages,
            pageNumber,
            pageSize,
            filters,
            [
                new("displayOrder", "asc", "Display order"),
                new("createdAtUtc", "desc", "Newest created"),
                new("nameAr", "asc", "Arabic name"),
                new("nameEn", "asc", "English name")
            ],
            facets);
    }

    private static int ResolveLevel(Category category, List<Category> categories)
    {
        var level = 0;
        var currentParentId = category.ParentCategoryId;

        while (currentParentId.HasValue)
        {
            level++;
            currentParentId = categories.FirstOrDefault(item => item.Id == currentParentId.Value)?.ParentCategoryId;
        }

        return level;
    }

    private static DateTime ToInclusiveUpperBound(DateTime value)
    {
        return value.TimeOfDay == TimeSpan.Zero
            ? value.Date.AddDays(1).AddTicks(-1)
            : value;
    }

    private static IEnumerable<SearchCategoryProjection> ApplySorting(IEnumerable<SearchCategoryProjection> items, string? sortField, string? sortDirection)
    {
        var normalizedField = sortField?.Trim().ToLowerInvariant();
        var descending = !string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        return normalizedField switch
        {
            "createdatutc" or "createdat" => descending ? items.OrderByDescending(item => item.Entity.CreatedAtUtc) : items.OrderBy(item => item.Entity.CreatedAtUtc),
            "namear" => descending ? items.OrderByDescending(item => item.Entity.NameAr) : items.OrderBy(item => item.Entity.NameAr),
            "nameen" => descending ? items.OrderByDescending(item => item.Entity.NameEn) : items.OrderBy(item => item.Entity.NameEn),
            _ => descending
                ? items.OrderByDescending(item => item.Entity.DisplayOrder).ThenByDescending(item => item.Entity.CreatedAtUtc)
                : items.OrderBy(item => item.Entity.DisplayOrder).ThenBy(item => item.Entity.CreatedAtUtc)
        };
    }

    private sealed record SearchCategoryProjection(
        Category Entity,
        int Level,
        bool HasChildren,
        string? ParentNameAr,
        string? ParentNameEn,
        int MasterProductsCount,
        int BrandsCount);
}
