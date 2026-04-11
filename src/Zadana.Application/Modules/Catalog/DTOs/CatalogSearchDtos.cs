using Zadana.Domain.Modules.Catalog.Enums;

namespace Zadana.Application.Modules.Catalog.DTOs;

public record SortDescriptorDto(
    string Field,
    string Direction,
    string Label);

public record CatalogFacetCountDto(
    string Key,
    string? LabelAr,
    string? LabelEn,
    int Count);

public record CatalogSearchResponse<TItem, TFilters, TFacets>(
    IReadOnlyList<TItem> Items,
    int TotalCount,
    int TotalPages,
    int PageNumber,
    int PageSize,
    TFilters AppliedFilters,
    IReadOnlyList<SortDescriptorDto> AvailableSorts,
    TFacets? Facets);

public record ProductSearchFiltersDto(
    IReadOnlyList<Guid>? SubcategoryIds = null,
    IReadOnlyList<Guid>? BrandIds = null,
    IReadOnlyList<ProductStatus>? Statuses = null,
    bool? IsActiveBrand = null,
    bool? HasBrand = null);

public record CategorySearchFiltersDto(
    Guid? ParentCategoryId = null,
    int? Level = null,
    bool? IsActive = null,
    bool? HasChildren = null,
    DateTime? CreatedAtFrom = null,
    DateTime? CreatedAtTo = null);

public record BrandSearchFiltersDto(
    Guid? CategoryId = null,
    bool? IsActive = null,
    bool? HasProducts = null,
    DateTime? CreatedAtFrom = null,
    DateTime? CreatedAtTo = null);

public record ProductSearchFacetsDto(
    IReadOnlyList<CatalogFacetCountDto> Statuses,
    IReadOnlyList<CatalogFacetCountDto> Brands,
    IReadOnlyList<CatalogFacetCountDto> Categories);

public record CategorySearchFacetsDto(
    IReadOnlyList<CatalogFacetCountDto> Levels,
    int ActiveCount,
    int InactiveCount,
    int WithChildrenCount);

public record BrandSearchFacetsDto(
    int ActiveCount,
    int InactiveCount,
    int WithProductsCount);
