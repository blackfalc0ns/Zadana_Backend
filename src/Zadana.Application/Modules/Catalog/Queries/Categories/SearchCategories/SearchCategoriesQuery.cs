using MediatR;
using Zadana.Application.Modules.Catalog.DTOs;

namespace Zadana.Application.Modules.Catalog.Queries.Categories.SearchCategories;

public record SearchCategoriesQuery(
    string? Search,
    CategorySearchFiltersDto Filters,
    string? SortField = null,
    string? SortDirection = null,
    int PageNumber = 1,
    int PageSize = 10)
    : IRequest<CatalogSearchResponse<CategoryDto, CategorySearchFiltersDto, CategorySearchFacetsDto>>;
