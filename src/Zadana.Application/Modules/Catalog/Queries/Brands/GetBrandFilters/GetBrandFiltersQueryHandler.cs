using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Zadana.Application.Common.Caching;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Settings;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Application.Modules.Catalog.Queries;
using Zadana.Application.Modules.Catalog.Queries.Brands;
using Zadana.Application.Modules.Vendors.Support;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Queries.Brands.GetBrandFilters;

public class GetBrandFiltersQueryHandler : IRequestHandler<GetBrandFiltersQuery, BrandFiltersDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IAppCache _cache;
    private readonly CacheDurationSettings _durations;

    public GetBrandFiltersQueryHandler(
        IApplicationDbContext context,
        IAppCache cache,
        IOptions<CachingSettings> cachingOptions)
    {
        _context = context;
        _cache = cache;
        _durations = cachingOptions.Value.Durations;
    }

    public async Task<BrandFiltersDto> Handle(GetBrandFiltersQuery request, CancellationToken cancellationToken)
    {
        return await _cache.GetOrCreateAsync(
            CatalogQueryCacheKeys.BrandFilters(request.BrandId),
            async token =>
            {
                var brand = await _context.Brands
                    .AsNoTracking()
                    .Where(item => item.Id == request.BrandId && item.IsActive)
                    .Select(item => new { item.Id, item.NameAr, item.NameEn, item.LogoUrl })
                    .FirstOrDefaultAsync(token)
                    ?? throw new NotFoundException(nameof(Brand), request.BrandId);

                var categories = await _context.Categories
                    .AsNoTracking()
                    .Where(category => category.IsActive)
                    .Select(category => new CategoryRow(
                        category.Id,
                        category.ParentCategoryId,
                        category.NameAr,
                        category.NameEn,
                        category.ImageUrl,
                        category.DisplayOrder))
                    .ToListAsync(token);

                var categoriesById = categories.ToDictionary(category => category.Id);

                var scopedMasterProducts = await _context.MasterProducts
                    .AsNoTracking()
                    .Where(product =>
                        product.Status == ProductStatus.Active &&
                        product.BrandId == request.BrandId)
                    .Select(product => new ScopedMasterProductRow(
                        product.Id,
                        product.CategoryId,
                        product.MeasurementValue,
                        product.MeasurementUnitId,
                        product.PackageTypeId))
                    .ToListAsync(token);

                var visibleMasterProductRows = await _context.VendorProducts
                    .AsNoTracking()
                    .Where(product =>
                        product.Status == VendorProductStatus.Active &&
                        product.IsAvailable &&
                        product.StockQuantity > 0 &&
                        product.MasterProduct.Status == ProductStatus.Active &&
                        product.MasterProduct.BrandId == request.BrandId &&
                        product.Vendor.Status == VendorStatus.Active)
                    .Select(product => new VisibleMasterProductRow(
                        product.MasterProductId,
                        product.VendorId,
                        product.VendorBranchId,
                        product.VendorBranch != null && product.VendorBranch.IsPrimary,
                        product.SellingPrice,
                        product.CompareAtPrice,
                        product.CreatedAtUtc))
                    .ToListAsync(token);

                visibleMasterProductRows = ApplyUnifiedPricing(visibleMasterProductRows);

                var availabilityDecisions = await VendorCustomerAvailabilityPolicy.LoadDecisionsAsync(
                    _context,
                    visibleMasterProductRows.Select(product => product.VendorId),
                    token);

                var visibleMasterProductIds = visibleMasterProductRows
                    .Where(product => VendorCustomerAvailabilityPolicy.ResolveOrOffline(availabilityDecisions, product.VendorId).IsVisibleInCatalog)
                    .Select(product => product.MasterProductId)
                    .ToHashSet();

                scopedMasterProducts = scopedMasterProducts
                    .Where(product => visibleMasterProductIds.Contains(product.MasterProductId))
                    .ToList();

                var activeCategoryIds = scopedMasterProducts
                    .Select(product => product.CategoryId)
                    .Where(categoriesById.ContainsKey)
                    .Distinct()
                    .ToList();

                var unitsIds = scopedMasterProducts
                    .Where(product => product.MeasurementUnitId.HasValue)
                    .Select(product => product.MeasurementUnitId!.Value)
                    .Distinct()
                    .ToList();

                var packageTypeIds = scopedMasterProducts
                    .Where(product => product.PackageTypeId.HasValue)
                    .Select(product => product.PackageTypeId!.Value)
                    .Distinct()
                    .ToList();

                var measurementValues = scopedMasterProducts
                    .Where(product => product.MeasurementValue.HasValue)
                    .Select(product => product.MeasurementValue!.Value)
                    .Distinct()
                    .OrderBy(value => value)
                    .ToList();

                var categoryItems = new Dictionary<Guid, BrandFilterCategoryItemDto>();
                var subcategoryItems = new Dictionary<Guid, BrandFilterSubcategoryItemDto>();

                foreach (var categoryId in activeCategoryIds)
                {
                    var category = categoriesById[categoryId];

                    if (category.ParentCategoryId.HasValue && categoriesById.TryGetValue(category.ParentCategoryId.Value, out var parent))
                    {
                        categoryItems[parent.Id] = new BrandFilterCategoryItemDto(
                            parent.Id,
                            BrandCatalogQueryHelpers.PickLocalized(parent.NameAr, parent.NameEn),
                            parent.ImageUrl);

                        subcategoryItems[category.Id] = new BrandFilterSubcategoryItemDto(
                            category.Id,
                            BrandCatalogQueryHelpers.PickLocalized(category.NameAr, category.NameEn),
                            parent.Id,
                            category.ImageUrl);
                    }
                    else
                    {
                        categoryItems[category.Id] = new BrandFilterCategoryItemDto(
                            category.Id,
                            BrandCatalogQueryHelpers.PickLocalized(category.NameAr, category.NameEn),
                            category.ImageUrl);
                    }
                }

                var unitRows = await _context.UnitsOfMeasure
                    .AsNoTracking()
                    .Where(unit => unit.IsActive && unit.Kind == UnitKind.Measurement && unitsIds.Contains(unit.Id))
                    .Select(unit => new UnitRow(
                        unit.Id,
                        unit.NameAr,
                        unit.NameEn))
                    .ToListAsync(token);

                var units = unitRows
                    .Select(unit => new CatalogFilterNamedItemDto(
                        unit.Id,
                        BrandCatalogQueryHelpers.PickLocalized(unit.NameAr, unit.NameEn)))
                    .OrderBy(unit => unit.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();

                var packageTypeRows = await _context.UnitsOfMeasure
                    .AsNoTracking()
                    .Where(unit => unit.IsActive && unit.Kind == UnitKind.Packaging && packageTypeIds.Contains(unit.Id))
                    .Select(unit => new UnitRow(
                        unit.Id,
                        unit.NameAr,
                        unit.NameEn))
                    .ToListAsync(token);

                var packageTypes = packageTypeRows
                    .Select(unit => new CatalogFilterNamedItemDto(
                        unit.Id,
                        BrandCatalogQueryHelpers.PickLocalized(unit.NameAr, unit.NameEn)))
                    .OrderBy(unit => unit.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();

                var visiblePrices = await _context.VendorProducts
                    .AsNoTracking()
                    .Where(product =>
                        product.Status == VendorProductStatus.Active &&
                        product.IsAvailable &&
                        product.StockQuantity > 0 &&
                        product.MasterProduct.Status == ProductStatus.Active &&
                        product.MasterProduct.BrandId == request.BrandId &&
                        product.Vendor.Status == VendorStatus.Active)
                    .Select(product => new VisiblePriceRow(
                        product.MasterProductId,
                        product.VendorId,
                        product.VendorBranchId,
                        product.VendorBranch != null && product.VendorBranch.IsPrimary,
                        product.SellingPrice,
                        product.CompareAtPrice,
                        product.CreatedAtUtc))
                    .ToListAsync(token);

                visiblePrices = ApplyUnifiedPricing(visiblePrices);

                var filteredPrices = visiblePrices
                    .Where(product => VendorCustomerAvailabilityPolicy.ResolveOrOffline(availabilityDecisions, product.VendorId).IsVisibleInCatalog)
                    .Select(product => product.SellingPrice)
                    .ToList();

                var priceRange = filteredPrices.Count == 0
                    ? new CatalogFilterPriceRangeDto(0, 0)
                    : new CatalogFilterPriceRangeDto(filteredPrices.Min(), filteredPrices.Max());

                return new BrandFiltersDto(
                    new BrandFilterBrandItemDto(
                        brand.Id,
                        BrandCatalogQueryHelpers.PickLocalized(brand.NameAr, brand.NameEn),
                        brand.LogoUrl),
                    categoryItems.Values
                        .OrderBy(item => categoriesById[item.Id].DisplayOrder)
                        .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                        .ToList(),
                    subcategoryItems.Values
                        .OrderBy(item => categoriesById[item.CategoryId].DisplayOrder)
                        .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                        .ToList(),
                    units,
                    packageTypes,
                    measurementValues,
                    priceRange,
                    BrandCatalogQueryHelpers.BuildSortOptions());
            },
            new AppCacheEntryOptions(_durations.PublicCatalogMetadata),
            [CacheTagNames.CatalogFilters],
            cancellationToken);
    }

    private sealed record CategoryRow(
        Guid Id,
        Guid? ParentCategoryId,
        string? NameAr,
        string? NameEn,
        string? ImageUrl,
        int DisplayOrder);

    private sealed record ScopedMasterProductRow(
        Guid MasterProductId,
        Guid CategoryId,
        decimal? MeasurementValue,
        Guid? MeasurementUnitId,
        Guid? PackageTypeId);

    private sealed record UnitRow(
        Guid Id,
        string? NameAr,
        string? NameEn);

    private static List<VisibleMasterProductRow> ApplyUnifiedPricing(List<VisibleMasterProductRow> rows) =>
        rows
            .GroupBy(row => new { row.VendorId, row.MasterProductId })
            .SelectMany(group =>
            {
                var canonical = group
                    .OrderByDescending(row => row.IsPrimaryBranch)
                    .ThenBy(row => row.VendorBranchId.HasValue)
                    .ThenBy(row => row.CreatedAtUtc)
                    .First();

                return group.Select(row => row with
                {
                    SellingPrice = canonical.SellingPrice,
                    CompareAtPrice = canonical.CompareAtPrice
                });
            })
            .ToList();

    private static List<VisiblePriceRow> ApplyUnifiedPricing(List<VisiblePriceRow> rows) =>
        rows
            .GroupBy(row => new { row.VendorId, row.MasterProductId })
            .SelectMany(group =>
            {
                var canonical = group
                    .OrderByDescending(row => row.IsPrimaryBranch)
                    .ThenBy(row => row.VendorBranchId.HasValue)
                    .ThenBy(row => row.CreatedAtUtc)
                    .First();

                return group.Select(row => row with
                {
                    SellingPrice = canonical.SellingPrice,
                    CompareAtPrice = canonical.CompareAtPrice
                });
            })
            .ToList();

    private sealed record VisiblePriceRow(
        Guid MasterProductId,
        Guid VendorId,
        Guid? VendorBranchId,
        bool IsPrimaryBranch,
        decimal SellingPrice,
        decimal? CompareAtPrice,
        DateTime CreatedAtUtc);

    private sealed record VisibleMasterProductRow(
        Guid MasterProductId,
        Guid VendorId,
        Guid? VendorBranchId,
        bool IsPrimaryBranch,
        decimal SellingPrice,
        decimal? CompareAtPrice,
        DateTime CreatedAtUtc);
}
