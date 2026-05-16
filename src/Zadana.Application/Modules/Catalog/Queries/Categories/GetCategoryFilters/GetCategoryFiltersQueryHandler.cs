using System.Globalization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Zadana.Application.Common.Caching;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Settings;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Application.Modules.Catalog.Queries;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Queries.Categories.GetCategoryFilters;

public class GetCategoryFiltersQueryHandler : IRequestHandler<GetCategoryFiltersQuery, CategoryFiltersDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IAppCache _cache;
    private readonly CacheDurationSettings _durations;

    public GetCategoryFiltersQueryHandler(
        IApplicationDbContext context,
        IAppCache cache,
        IOptions<CachingSettings> cachingOptions)
    {
        _context = context;
        _cache = cache;
        _durations = cachingOptions.Value.Durations;
    }

    public async Task<CategoryFiltersDto> Handle(GetCategoryFiltersQuery request, CancellationToken cancellationToken)
    {
        return await _cache.GetOrCreateAsync(
            CatalogQueryCacheKeys.CategoryFilters(request.CategoryId),
            async token =>
            {
                var categories = await _context.Categories
                    .AsNoTracking()
                    .Select(category => new CategoryScopeRow(
                        category.Id,
                        category.ParentCategoryId,
                        category.NameAr,
                        category.NameEn,
                        category.ImageUrl,
                        category.DisplayOrder,
                        category.IsActive))
                    .ToListAsync(token);

                var scope = CatalogFilterScopeResolver.Resolve(request.CategoryId, categories)
                    ?? throw new NotFoundException(nameof(Category), request.CategoryId);

                var subtreeIdSet = scope.ActiveSubtreeIds.ToHashSet();

                var masterProducts = await _context.MasterProducts
                    .AsNoTracking()
                    .Where(product => product.Status == ProductStatus.Active)
                    .Select(product => new ScopedMasterProductRow(
                        product.CategoryId,
                        product.ProductTypeId,
                        product.PartId,
                        product.BrandId,
                        product.MeasurementValue,
                        product.MeasurementUnitId,
                        product.PackageTypeId))
                    .ToListAsync(token);

                var scopedMasterProducts = masterProducts
                    .Where(product => subtreeIdSet.Contains(product.CategoryId))
                    .ToList();

                var brandIds = scopedMasterProducts
                    .Where(product => product.BrandId.HasValue)
                    .Select(product => product.BrandId!.Value)
                    .Distinct()
                    .ToList();

                var productTypeIds = scopedMasterProducts
                    .Where(product => product.ProductTypeId.HasValue)
                    .Select(product => product.ProductTypeId!.Value)
                    .Distinct()
                    .ToList();

                var partIds = scopedMasterProducts
                    .Where(product => product.PartId.HasValue)
                    .Select(product => product.PartId!.Value)
                    .Distinct()
                    .ToList();

                var unitIds = scopedMasterProducts
                    .Where(product => product.MeasurementUnitId.HasValue)
                    .Select(product => product.MeasurementUnitId!.Value)
                    .Distinct()
                    .ToList();

                var measurementValues = scopedMasterProducts
                    .Where(product => product.MeasurementValue.HasValue)
                    .Select(product => product.MeasurementValue!.Value)
                    .Distinct()
                    .OrderBy(value => value)
                    .ToList();

                var packageTypeIds = scopedMasterProducts
                    .Where(product => product.PackageTypeId.HasValue)
                    .Select(product => product.PackageTypeId!.Value)
                    .Distinct()
                    .ToList();

                var brandRows = await _context.Brands
                    .AsNoTracking()
                    .Where(brand => brand.IsActive && brandIds.Contains(brand.Id))
                    .Select(brand => new RawBrandRow(
                        brand.Id,
                        brand.NameAr,
                        brand.NameEn,
                        brand.LogoUrl))
                    .ToListAsync(token);

                var subcategories = scope.DirectActiveChildren
                    .OrderBy(child => child.DisplayOrder)
                    .ThenBy(child => PickLocalized(child.NameAr, child.NameEn), StringComparer.CurrentCultureIgnoreCase)
                    .Select(child => new CategoryFilterCategoryItemDto(
                        child.Id,
                        PickLocalized(child.NameAr, child.NameEn),
                        child.ImageUrl))
                    .ToList();

                var productTypeRows = await _context.ProductTypes
                    .AsNoTracking()
                    .Where(productType => productType.IsActive && productTypeIds.Contains(productType.Id))
                    .Select(productType => new RawNamedRow(
                        productType.Id,
                        productType.NameAr,
                        productType.NameEn))
                    .ToListAsync(token);

                var productTypes = productTypeRows
                    .Select(item => new CatalogFilterNamedItemDto(
                        item.Id,
                        PickLocalized(item.NameAr, item.NameEn)))
                    .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();

                var partRows = await _context.Parts
                    .AsNoTracking()
                    .Where(part => part.IsActive && partIds.Contains(part.Id))
                    .Select(part => new RawPartRow(
                        part.Id,
                        part.NameAr,
                        part.NameEn,
                        part.ProductTypeId))
                    .ToListAsync(token);

                var parts = partRows
                    .Select(item => new CatalogFilterPartItemDto(
                        item.Id,
                        PickLocalized(item.NameAr, item.NameEn),
                        item.ProductTypeId))
                    .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();

                var brands = brandRows
                    .Select(item => new CatalogFilterBrandItemDto(
                        item.Id,
                        PickLocalized(item.NameAr, item.NameEn),
                        item.LogoUrl))
                    .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();

                var quantityRows = await _context.UnitsOfMeasure
                    .AsNoTracking()
                    .Where(unit => unit.IsActive && unit.Kind == UnitKind.Measurement && unitIds.Contains(unit.Id))
                    .Select(unit => new RawNamedRow(
                        unit.Id,
                        unit.NameAr,
                        unit.NameEn))
                    .ToListAsync(token);

                var quantities = quantityRows
                    .Select(unit => new CatalogFilterNamedItemDto(
                        unit.Id,
                        PickLocalized(unit.NameAr, unit.NameEn)))
                    .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();

                var packageTypeRows = await _context.UnitsOfMeasure
                    .AsNoTracking()
                    .Where(unit => unit.IsActive && unit.Kind == UnitKind.Packaging && packageTypeIds.Contains(unit.Id))
                    .Select(unit => new RawNamedRow(
                        unit.Id,
                        unit.NameAr,
                        unit.NameEn))
                    .ToListAsync(token);

                var packageTypes = packageTypeRows
                    .Select(unit => new CatalogFilterNamedItemDto(
                        unit.Id,
                        PickLocalized(unit.NameAr, unit.NameEn)))
                    .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();

                var visiblePriceRows = await _context.VendorProducts
                    .AsNoTracking()
                    .Where(product =>
                        product.Status == VendorProductStatus.Active &&
                        product.IsAvailable &&
                        product.StockQuantity > 0 &&
                        product.MasterProduct.Status == ProductStatus.Active &&
                        product.Vendor.Status == VendorStatus.Active &&
                        product.Vendor.AcceptOrders)
                    .Select(product => new VisiblePriceRow(
                        product.MasterProduct.CategoryId,
                        product.SellingPrice))
                    .ToListAsync(token);

                var visiblePrices = visiblePriceRows
                    .Where(product => subtreeIdSet.Contains(product.CategoryId))
                    .Select(product => product.SellingPrice)
                    .ToList();

                var priceRange = visiblePrices.Count == 0
                    ? new CatalogFilterPriceRangeDto(0, 0)
                    : new CatalogFilterPriceRangeDto(visiblePrices.Min(), visiblePrices.Max());

                return new CategoryFiltersDto(
                    new CategoryFilterCategoryItemDto(
                        scope.Category.Id,
                        PickLocalized(scope.Category.NameAr, scope.Category.NameEn),
                        scope.Category.ImageUrl),
                    subcategories,
                    productTypes,
                    parts,
                    quantities,
                    packageTypes,
                    measurementValues,
                    brands,
                    priceRange,
                    BuildSortOptions());
            },
            new AppCacheEntryOptions(_durations.PublicCatalogMetadata),
            [CacheTagNames.CatalogFilters],
            cancellationToken);
    }

    private static IReadOnlyList<CatalogSortOptionDto> BuildSortOptions() =>
        IsArabic()
            ? new[]
            {
                new CatalogSortOptionDto("\u0627\u0644\u0623\u062D\u062F\u062B", "newest"),
                new CatalogSortOptionDto("\u0627\u0644\u0633\u0639\u0631: \u0645\u0646 \u0627\u0644\u0623\u0642\u0644 \u0625\u0644\u0649 \u0627\u0644\u0623\u0639\u0644\u0649", "price_low_high"),
                new CatalogSortOptionDto("\u0627\u0644\u0633\u0639\u0631: \u0645\u0646 \u0627\u0644\u0623\u0639\u0644\u0649 \u0625\u0644\u0649 \u0627\u0644\u0623\u0642\u0644", "price_high_low"),
                new CatalogSortOptionDto("\u0627\u0644\u0623\u0643\u062B\u0631 \u0645\u0628\u064A\u0639\u064B\u0627", "best_selling"),
                new CatalogSortOptionDto("\u0627\u0644\u0623\u0639\u0644\u0649 \u062A\u0642\u064A\u064A\u0645\u064B\u0627", "highest_rated"),
                new CatalogSortOptionDto("\u0623\u0628\u062C\u062F\u064A\u064B\u0627", "alphabetical")
            }
            : new[]
            {
                new CatalogSortOptionDto("Newest", "newest"),
                new CatalogSortOptionDto("Price: Low to High", "price_low_high"),
                new CatalogSortOptionDto("Price: High to Low", "price_high_low"),
                new CatalogSortOptionDto("Best Selling", "best_selling"),
                new CatalogSortOptionDto("Highest Rated", "highest_rated"),
                new CatalogSortOptionDto("Alphabetical", "alphabetical")
            };

    private static bool IsArabic() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase);

    private static string PickLocalized(string? arabic, string? english)
    {
        var preferred = IsArabic() ? arabic : english;
        var fallback = IsArabic() ? english : arabic;
        return preferred?.Trim()
            ?? fallback?.Trim()
            ?? string.Empty;
    }

    private sealed record ScopedMasterProductRow(
        Guid CategoryId,
        Guid? ProductTypeId,
        Guid? PartId,
        Guid? BrandId,
        decimal? MeasurementValue,
        Guid? MeasurementUnitId,
        Guid? PackageTypeId);

    private sealed record RawBrandRow(
        Guid Id,
        string? NameAr,
        string? NameEn,
        string? LogoUrl);

    private sealed record RawNamedRow(
        Guid Id,
        string? NameAr,
        string? NameEn);

    private sealed record RawPartRow(
        Guid Id,
        string? NameAr,
        string? NameEn,
        Guid ProductTypeId);

    private sealed record VisiblePriceRow(
        Guid CategoryId,
        decimal SellingPrice);
}
