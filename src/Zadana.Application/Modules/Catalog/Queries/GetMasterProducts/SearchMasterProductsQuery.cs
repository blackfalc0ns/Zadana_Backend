using MediatR;
using Zadana.Application.Modules.Catalog.DTOs;

namespace Zadana.Application.Modules.Catalog.Queries.GetMasterProducts;

public record SearchMasterProductsQuery(
    string? Search,
    ProductSearchFiltersDto Filters,
    string? SortField = null,
    string? SortDirection = null,
    int PageNumber = 1,
    int PageSize = 10)
    : IRequest<CatalogSearchResponse<MasterProductDto, ProductSearchFiltersDto, ProductSearchFacetsDto>>;
