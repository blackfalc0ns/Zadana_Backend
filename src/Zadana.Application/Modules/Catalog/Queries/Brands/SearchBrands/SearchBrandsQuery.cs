using MediatR;
using Zadana.Application.Modules.Catalog.DTOs;

namespace Zadana.Application.Modules.Catalog.Queries.Brands.SearchBrands;

public record SearchBrandsQuery(
    string? Search,
    BrandSearchFiltersDto Filters,
    string? SortField = null,
    string? SortDirection = null,
    int PageNumber = 1,
    int PageSize = 10)
    : IRequest<CatalogSearchResponse<BrandDto, BrandSearchFiltersDto, BrandSearchFacetsDto>>;
