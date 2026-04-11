using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Catalog.Enums;

namespace Zadana.Application.Modules.Catalog.Queries.GetMasterProducts;

public class SearchMasterProductsQueryHandler : IRequestHandler<SearchMasterProductsQuery, CatalogSearchResponse<MasterProductDto, ProductSearchFiltersDto, ProductSearchFacetsDto>>
{
    private readonly IApplicationDbContext _context;

    public SearchMasterProductsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CatalogSearchResponse<MasterProductDto, ProductSearchFiltersDto, ProductSearchFacetsDto>> Handle(SearchMasterProductsQuery request, CancellationToken cancellationToken)
    {
        var normalizedFilters = NormalizeFilters(request.Filters);
        var query = _context.MasterProducts
            .AsNoTracking()
            .Include(p => p.Images)
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .Include(p => p.UnitOfMeasure)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var searchTerm = request.Search.Trim();
            query = query.Where(p =>
                p.NameAr.Contains(searchTerm) ||
                p.NameEn.Contains(searchTerm) ||
                p.Slug.Contains(searchTerm) ||
                (p.Barcode != null && p.Barcode.Contains(searchTerm)) ||
                (p.DescriptionAr != null && p.DescriptionAr.Contains(searchTerm)) ||
                (p.DescriptionEn != null && p.DescriptionEn.Contains(searchTerm)));
        }

        if (normalizedFilters.SubcategoryIds is { Count: > 0 })
        {
            query = query.Where(p => normalizedFilters.SubcategoryIds.Contains(p.CategoryId));
        }

        if (normalizedFilters.BrandIds is { Count: > 0 })
        {
            query = query.Where(p => p.BrandId.HasValue && normalizedFilters.BrandIds.Contains(p.BrandId.Value));
        }

        if (normalizedFilters.Statuses is { Count: > 0 })
        {
            query = query.Where(p => normalizedFilters.Statuses.Contains(p.Status));
        }

        if (normalizedFilters.HasBrand.HasValue)
        {
            query = normalizedFilters.HasBrand.Value
                ? query.Where(p => p.BrandId != null)
                : query.Where(p => p.BrandId == null);
        }

        if (normalizedFilters.IsActiveBrand.HasValue)
        {
            query = query.Where(p =>
                p.BrandId != null &&
                p.Brand != null &&
                p.Brand.IsActive == normalizedFilters.IsActiveBrand.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        var pagedQuery = ApplyProductSorting(query, request.SortField, request.SortDirection)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize);

        var pagedProducts = await pagedQuery.ToListAsync(cancellationToken);
        var items = pagedProducts
            .Select(product => MapMasterProductDto(product, false))
            .ToList();

        var facets = new ProductSearchFacetsDto(
            await query.GroupBy(p => p.Status)
                .Select(group => new CatalogFacetCountDto(
                    group.Key.ToString(),
                    MapStatusLabelAr(group.Key),
                    MapStatusLabelEn(group.Key),
                    group.Count()))
                .ToListAsync(cancellationToken),
            await query.Where(p => p.BrandId != null && p.Brand != null)
                .GroupBy(p => new { p.BrandId, p.Brand!.NameAr, p.Brand!.NameEn })
                .Select(group => new CatalogFacetCountDto(
                    group.Key.BrandId!.Value.ToString(),
                    group.Key.NameAr,
                    group.Key.NameEn,
                    group.Count()))
                .ToListAsync(cancellationToken),
            await query.GroupBy(p => new { p.CategoryId, p.Category.NameAr, p.Category.NameEn })
                .Select(group => new CatalogFacetCountDto(
                    group.Key.CategoryId.ToString(),
                    group.Key.NameAr,
                    group.Key.NameEn,
                    group.Count()))
                .ToListAsync(cancellationToken));

        return new CatalogSearchResponse<MasterProductDto, ProductSearchFiltersDto, ProductSearchFacetsDto>(
            items,
            totalCount,
            totalPages,
            pageNumber,
            pageSize,
            normalizedFilters,
            GetProductSortOptions(),
            facets);
    }

    private static ProductSearchFiltersDto NormalizeFilters(ProductSearchFiltersDto? filters)
    {
        return filters ?? new ProductSearchFiltersDto();
    }

    private static IQueryable<MasterProduct> ApplyProductSorting(IQueryable<MasterProduct> query, string? sortField, string? sortDirection)
    {
        var normalizedField = sortField?.Trim().ToLowerInvariant();
        var isDescending = !string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        return normalizedField switch
        {
            "createdatutc" or "createdat" => isDescending ? query.OrderByDescending(p => p.CreatedAtUtc) : query.OrderBy(p => p.CreatedAtUtc),
            "updatedatutc" or "updatedat" => isDescending ? query.OrderByDescending(p => p.UpdatedAtUtc) : query.OrderBy(p => p.UpdatedAtUtc),
            "namear" => isDescending ? query.OrderByDescending(p => p.NameAr) : query.OrderBy(p => p.NameAr),
            "nameen" => isDescending ? query.OrderByDescending(p => p.NameEn) : query.OrderBy(p => p.NameEn),
            "status" => isDescending ? query.OrderByDescending(p => p.Status) : query.OrderBy(p => p.Status),
            _ => query.OrderByDescending(p => p.UpdatedAtUtc).ThenByDescending(p => p.CreatedAtUtc)
        };
    }

    private static MasterProductDto MapMasterProductDto(MasterProduct product, bool isInVendorStore)
    {
        return new MasterProductDto(
            product.Id,
            product.NameAr,
            product.NameEn,
            product.Slug,
            product.DescriptionAr,
            product.DescriptionEn,
            product.Barcode,
            product.CategoryId,
            product.BrandId,
            product.Brand?.NameAr,
            product.Brand?.NameEn,
            product.UnitOfMeasureId,
            product.UnitOfMeasure?.NameAr,
            product.UnitOfMeasure?.NameEn,
            product.Status.ToString(),
            isInVendorStore,
            product.Images.Select(i => new MasterProductImageDto(i.Url, i.AltText, i.DisplayOrder, i.IsPrimary)).ToList(),
            product.CreatedAtUtc,
            product.UpdatedAtUtc
        );
    }

    private static List<SortDescriptorDto> GetProductSortOptions()
    {
        return
        [
            new("updatedAtUtc", "desc", "Newest updated"),
            new("createdAtUtc", "desc", "Newest created"),
            new("nameAr", "asc", "Arabic name"),
            new("nameEn", "asc", "English name"),
            new("status", "asc", "Status")
        ];
    }

    private static string MapStatusLabelAr(ProductStatus status)
    {
        return status switch
        {
            ProductStatus.Active => "نشط",
            ProductStatus.Draft => "مسودة",
            ProductStatus.Inactive => "غير نشط",
            ProductStatus.Discontinued => "متوقف",
            _ => status.ToString()
        };
    }

    private static string MapStatusLabelEn(ProductStatus status)
    {
        return status switch
        {
            ProductStatus.Active => "Active",
            ProductStatus.Draft => "Draft",
            ProductStatus.Inactive => "Inactive",
            ProductStatus.Discontinued => "Discontinued",
            _ => status.ToString()
        };
    }
}
