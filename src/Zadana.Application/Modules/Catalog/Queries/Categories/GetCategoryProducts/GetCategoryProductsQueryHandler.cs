using System.Globalization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Zadana.Application.Common.Caching;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Settings;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Application.Modules.Catalog.Interfaces;
using Zadana.Application.Modules.Catalog.Queries;
using Zadana.Application.Modules.Vendors.Support;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Queries.Categories.GetCategoryProducts;

public class GetCategoryProductsQueryHandler : IRequestHandler<GetCategoryProductsQuery, CategoryProductsDto>
{
    private const int DefaultPage = 1;
    private const int DefaultPerPage = 20;
    private const int MaxPerPage = 100;

    private readonly IApplicationDbContext _context;
    private readonly IAppCache _cache;
    private readonly ICatalogReadCacheService _catalogReadCacheService;
    private readonly CacheDurationSettings _durations;

    public GetCategoryProductsQueryHandler(
        IApplicationDbContext context,
        IAppCache cache,
        ICatalogReadCacheService catalogReadCacheService,
        IOptions<CachingSettings> cachingOptions)
    {
        _context = context;
        _cache = cache;
        _catalogReadCacheService = catalogReadCacheService;
        _durations = cachingOptions.Value.Durations;
    }

    public async Task<CategoryProductsDto> Handle(GetCategoryProductsQuery request, CancellationToken cancellationToken)
    {
        var page = NormalizePage(request.Page);
        var perPage = NormalizePerPage(request.PerPage);
        var baseResponse = await _cache.GetOrCreateAsync(
            CatalogQueryCacheKeys.CategoryProducts(
                request.CategoryId,
                request.SubcategoryId,
                request.ProductTypeId,
                request.PartId,
                request.QuantityId,
                request.MeasurementValue,
                request.PackageTypeId,
                request.BrandId,
                request.MinPrice,
                request.MaxPrice,
                request.Sort,
                page,
                perPage),
            token => BuildBaseResponseAsync(request, page, perPage, token),
            new AppCacheEntryOptions(_durations.BrowseBase),
            [CacheTagNames.Catalog],
            cancellationToken);

        var favoriteMasterProductIds = await _catalogReadCacheService.GetCurrentFavoriteMasterProductIdsAsync(cancellationToken);
        return CatalogQueryFavoriteOverlays.ApplyFavorites(baseResponse, favoriteMasterProductIds);
    }

    private async Task<CategoryProductsDto> BuildBaseResponseAsync(
        GetCategoryProductsQuery request,
        int page,
        int perPage,
        CancellationToken cancellationToken)
    {
        HashSet<Guid>? categoryScopeIds = null;
        Guid? effectiveCategoryId = request.CategoryId;
        Guid? effectiveSubcategoryId = request.SubcategoryId;
        CategoryProductsBreadcrumbDto? breadcrumb = null;

        if (request.CategoryId.HasValue || request.SubcategoryId.HasValue)
        {
            var categories = await _context.Categories
                .AsNoTracking()
                .Select(category => new CategoryScopeRow(
                    category.Id,
                    category.ParentCategoryId,
                    category.NameAr,
                    category.NameEn,
                    category.DisplayOrder,
                    category.IsActive))
                .ToListAsync(cancellationToken);

            var categoriesById = categories.ToDictionary(c => c.Id);

            if (request.SubcategoryId.HasValue)
            {
                var subcategoryScope = ResolveScope(request.SubcategoryId.Value, categories)
                    ?? throw new NotFoundException(nameof(Category), request.SubcategoryId.Value);

                categoryScopeIds = subcategoryScope.ActiveSubtreeIds.ToHashSet();

                var subcategoryParentId = subcategoryScope.Category.ParentCategoryId;
                effectiveCategoryId ??= subcategoryParentId ?? request.SubcategoryId.Value;

                // Build breadcrumb: category -> subcategory
                var subcategoryInfo = new CategoryProductsCategoryInfoDto(
                    subcategoryScope.Category.Id,
                    PickLocalized(subcategoryScope.Category.NameAr, subcategoryScope.Category.NameEn));

                CategoryProductsCategoryInfoDto? categoryInfo = null;
                if (subcategoryParentId.HasValue && categoriesById.TryGetValue(subcategoryParentId.Value, out var parentCategory))
                {
                    categoryInfo = new CategoryProductsCategoryInfoDto(
                        parentCategory.Id,
                        PickLocalized(parentCategory.NameAr, parentCategory.NameEn));
                }

                breadcrumb = new CategoryProductsBreadcrumbDto(categoryInfo, subcategoryInfo);

                if (request.CategoryId.HasValue)
                {
                    var categoryScope = ResolveScope(request.CategoryId.Value, categories)
                        ?? throw new NotFoundException(nameof(Category), request.CategoryId.Value);

                    if (!categoryScope.ActiveSubtreeIds.Contains(request.SubcategoryId.Value))
                    {
                        throw new NotFoundException(nameof(Category), request.SubcategoryId.Value);
                    }
                }
            }
            else if (request.CategoryId.HasValue)
            {
                var categoryScope = ResolveScope(request.CategoryId.Value, categories)
                    ?? throw new NotFoundException(nameof(Category), request.CategoryId.Value);

                categoryScopeIds = categoryScope.ActiveSubtreeIds.ToHashSet();

                // Build breadcrumb: category only
                var categoryInfo = new CategoryProductsCategoryInfoDto(
                    categoryScope.Category.Id,
                    PickLocalized(categoryScope.Category.NameAr, categoryScope.Category.NameEn));

                breadcrumb = new CategoryProductsBreadcrumbDto(categoryInfo, null);
            }
        }

        var salesByVendorProductId = await _catalogReadCacheService.GetDeliveredSalesByVendorProductIdAsync(cancellationToken);
        var reviewStatsByVendorId = await _catalogReadCacheService.GetVendorReviewStatsByVendorIdAsync(cancellationToken);

        var rawProducts = await _context.VendorProducts
            .AsNoTracking()
            .Where(product =>
                product.Status == VendorProductStatus.Active &&
                product.IsAvailable &&
                product.StockQuantity > 0 &&
                product.MasterProduct.Status == ProductStatus.Active &&
                product.Vendor.Status == VendorStatus.Active &&
                (categoryScopeIds == null || categoryScopeIds.Contains(product.MasterProduct.CategoryId)) &&
                (!request.ProductTypeId.HasValue || product.MasterProduct.ProductTypeId == request.ProductTypeId.Value) &&
                (!request.PartId.HasValue || product.MasterProduct.PartId == request.PartId.Value) &&
                (!request.BrandId.HasValue || product.MasterProduct.BrandId == request.BrandId.Value) &&
                (!request.QuantityId.HasValue || product.MasterProduct.MeasurementUnitId == request.QuantityId.Value) &&
                (!request.MeasurementValue.HasValue || product.MasterProduct.MeasurementValue == request.MeasurementValue.Value) &&
                (!request.PackageTypeId.HasValue || product.MasterProduct.PackageTypeId == request.PackageTypeId.Value) &&
                (!request.MinPrice.HasValue || product.SellingPrice >= request.MinPrice.Value) &&
                (!request.MaxPrice.HasValue || product.SellingPrice <= request.MaxPrice.Value))
            .Select(product => new RawCategoryProduct(
                product.Id,
                product.MasterProductId,
                product.CreatedAtUtc,
                product.VendorId,
                product.MasterProduct.CategoryId,
                !string.IsNullOrWhiteSpace(product.CustomNameAr) ? product.CustomNameAr : product.MasterProduct.NameAr,
                !string.IsNullOrWhiteSpace(product.CustomNameEn) ? product.CustomNameEn : product.MasterProduct.NameEn,
                product.Vendor.BusinessNameAr,
                product.Vendor.BusinessNameEn,
                product.SellingPrice,
                product.CompareAtPrice,
                product.MasterProduct.PackageType != null ? product.MasterProduct.PackageType.NameAr : null,
                product.MasterProduct.PackageType != null ? product.MasterProduct.PackageType.NameEn : null,
                product.MasterProduct.MeasurementValue,
                product.MasterProduct.MeasurementUnit != null ? product.MasterProduct.MeasurementUnit.NameAr : product.MasterProduct.UnitOfMeasure != null ? product.MasterProduct.UnitOfMeasure.NameAr : null,
                product.MasterProduct.MeasurementUnit != null ? product.MasterProduct.MeasurementUnit.NameEn : product.MasterProduct.UnitOfMeasure != null ? product.MasterProduct.UnitOfMeasure.NameEn : null,
                product.MasterProduct.MeasurementUnit != null ? product.MasterProduct.MeasurementUnit.Symbol : product.MasterProduct.UnitOfMeasure != null ? product.MasterProduct.UnitOfMeasure.Symbol : null,
                product.MasterProduct.Images
                    .OrderByDescending(image => image.IsPrimary)
                    .ThenBy(image => image.DisplayOrder)
                    .Select(image => image.Url)
                    .FirstOrDefault(),
                product.MasterProduct.VariantGroupId))
            .ToListAsync(cancellationToken);

        var availabilityDecisions = await VendorCustomerAvailabilityPolicy.LoadDecisionsAsync(
            _context,
            rawProducts.Select(product => product.VendorId),
            cancellationToken);

        rawProducts = rawProducts
            .Where(product => VendorCustomerAvailabilityPolicy.ResolveOrOffline(availabilityDecisions, product.VendorId).IsVisibleInCatalog)
            .ToList();

        var products = rawProducts
            .Select(product =>
            {
                salesByVendorProductId.TryGetValue(product.Id, out var salesCount);
                reviewStatsByVendorId.TryGetValue(product.VendorId, out var reviewStats);

                return new CategoryProductSource(
                    product.MasterProductId,
                    product.CreatedAtUtc,
                    product.CategoryId,
                    NormalizeText(product.NameAr),
                    NormalizeText(product.NameEn),
                    PickLocalized(product.NameAr, product.NameEn),
                    NormalizeText(product.StoreAr),
                    NormalizeText(product.StoreEn),
                    PickLocalized(product.StoreAr, product.StoreEn),
                    product.SellingPrice,
                    product.CompareAtPrice,
                    NormalizeText(product.PackageTypeAr),
                    NormalizeText(product.PackageTypeEn),
                    product.MeasurementValue,
                    NormalizeText(product.MeasurementUnitAr),
                    NormalizeText(product.MeasurementUnitEn),
                    NormalizeText(product.MeasurementUnitSymbol),
                    PickLocalizedNullable(
                        MasterProductDisplayDto.BuildDisplaySize(product.PackageTypeAr, product.MeasurementValue, product.MeasurementUnitAr, product.MeasurementUnitSymbol, true),
                        MasterProductDisplayDto.BuildDisplaySize(product.PackageTypeEn, product.MeasurementValue, product.MeasurementUnitEn, product.MeasurementUnitSymbol, false)),
                    product.ImageUrl,
                    salesCount,
                    reviewStats?.AverageRating,
                    reviewStats?.ReviewCount ?? 0,
                    product.VariantGroupId);
            })
            .GroupBy(product => product.VariantGroupId != default ? product.VariantGroupId : product.Id)
            .Select(group => group
                .OrderBy(product => product.SellingPrice)
                .ThenByDescending(product => product.CreatedAtUtc)
                .ThenBy(product => product.Store, StringComparer.CurrentCultureIgnoreCase)
                .First());

        var sortedProducts = ApplySorting(products, request.Sort).ToList();

        var total = sortedProducts.Count;
        var items = sortedProducts
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .Select(product => MapToProductItem(product, false))
            .ToList();

        return new CategoryProductsDto(
            breadcrumb,
            new CategoryProductsAppliedFiltersDto(
                effectiveCategoryId,
                effectiveSubcategoryId,
                request.ProductTypeId,
                request.PartId,
                request.QuantityId,
                request.MeasurementValue,
                request.PackageTypeId,
                request.BrandId,
                request.MinPrice,
                request.MaxPrice,
                NormalizeSort(request.Sort)),
            total,
            page,
            perPage,
            items);
    }

    private static int NormalizePage(int page) => page <= 0 ? DefaultPage : page;

    private static int NormalizePerPage(int perPage)
    {
        if (perPage <= 0)
        {
            return DefaultPerPage;
        }

        return Math.Min(perPage, MaxPerPage);
    }

    private IEnumerable<CategoryProductSource> ApplySorting(IEnumerable<CategoryProductSource> products, string? sort)
    {
        return NormalizeSort(sort) switch
        {
            "price_low_high" => products.OrderBy(product => product.SellingPrice)
                .ThenBy(product => product.Name, StringComparer.CurrentCultureIgnoreCase),
            "price_high_low" => products.OrderByDescending(product => product.SellingPrice)
                .ThenBy(product => product.Name, StringComparer.CurrentCultureIgnoreCase),
            "best_selling" => products.OrderByDescending(product => product.SalesCount)
                .ThenByDescending(product => product.Rating ?? 0)
                .ThenByDescending(product => product.CreatedAtUtc),
            "highest_rated" => products.OrderByDescending(product => product.Rating ?? 0)
                .ThenByDescending(product => product.ReviewCount)
                .ThenByDescending(product => product.SalesCount)
                .ThenByDescending(product => product.CreatedAtUtc),
            "alphabetical" => products.OrderBy(product => product.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenByDescending(product => product.CreatedAtUtc),
            _ => products.OrderByDescending(product => product.CreatedAtUtc)
                .ThenByDescending(product => product.SalesCount)
        };
    }

    private static string? NormalizeSort(string? sort)
    {
        var normalized = sort?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "newest" => "newest",
            "price_low_high" => "price_low_high",
            "price_high_low" => "price_high_low",
            "best_selling" => "best_selling",
            "highest_rated" => "highest_rated",
            "alphabetical" => "alphabetical",
            _ => null
        };
    }

    private CategoryProductItemDto MapToProductItem(CategoryProductSource product, bool isFavorite)
    {
        var isDiscounted = product.CompareAtPrice.HasValue && product.CompareAtPrice.Value > product.SellingPrice;

        return new CategoryProductItemDto(
            product.Id,
            product.Name,
            product.Store,
            product.SellingPrice,
            isDiscounted ? product.CompareAtPrice : null,
            product.ImageUrl,
            product.Rating,
            product.ReviewCount,
            FormatDiscount(product),
            isFavorite,
            product.Unit,
            isDiscounted);
    }

    private static decimal CalculateDiscountRate(CategoryProductSource product)
    {
        if (!product.CompareAtPrice.HasValue || product.CompareAtPrice.Value <= 0 || product.CompareAtPrice.Value <= product.SellingPrice)
        {
            return 0;
        }

        return (product.CompareAtPrice.Value - product.SellingPrice) / product.CompareAtPrice.Value;
    }

    private static string? FormatDiscount(CategoryProductSource product)
    {
        var rate = CalculateDiscountRate(product);
        return rate <= 0
            ? null
            : $"{Math.Round(rate * 100, MidpointRounding.AwayFromZero):0}%";
    }

    private static bool IsArabic() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase);

    private string PickLocalized(string? arabic, string? english)
    {
        var preferred = IsArabic() ? arabic : english;
        var fallback = IsArabic() ? english : arabic;
        return preferred?.Trim()
            ?? fallback?.Trim()
            ?? string.Empty;
    }

    private string? PickLocalizedNullable(string? arabic, string? english)
    {
        var value = PickLocalized(arabic, english);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static CategoryScope? ResolveScope(Guid categoryId, IReadOnlyCollection<CategoryScopeRow> categories)
    {
        var categoriesById = categories.ToDictionary(category => category.Id);
        if (!categoriesById.TryGetValue(categoryId, out var category) || !category.IsActive)
        {
            return null;
        }

        var activeChildrenByParent = categories
            .Where(child => child.IsActive && child.ParentCategoryId.HasValue)
            .GroupBy(child => child.ParentCategoryId!.Value)
            .ToDictionary(group => group.Key, group => group.ToArray());

        var activeSubtreeIds = new List<Guid>();
        var stack = new Stack<Guid>();
        stack.Push(category.Id);

        while (stack.Count > 0)
        {
            var currentId = stack.Pop();
            activeSubtreeIds.Add(currentId);

            if (!activeChildrenByParent.TryGetValue(currentId, out var children))
            {
                continue;
            }

            foreach (var child in children)
            {
                stack.Push(child.Id);
            }
        }

        return new CategoryScope(category, activeSubtreeIds);
    }

    private sealed record CategoryScope(
        CategoryScopeRow Category,
        IReadOnlyList<Guid> ActiveSubtreeIds);

    private sealed record CategoryScopeRow(
        Guid Id,
        Guid? ParentCategoryId,
        string? NameAr,
        string? NameEn,
        int DisplayOrder,
        bool IsActive);

    private sealed record RawCategoryProduct(
        Guid Id,
        Guid MasterProductId,
        DateTime CreatedAtUtc,
        Guid VendorId,
        Guid CategoryId,
        string? NameAr,
        string? NameEn,
        string StoreAr,
        string StoreEn,
        decimal SellingPrice,
        decimal? CompareAtPrice,
        string? PackageTypeAr,
        string? PackageTypeEn,
        decimal? MeasurementValue,
        string? MeasurementUnitAr,
        string? MeasurementUnitEn,
        string? MeasurementUnitSymbol,
        string? ImageUrl,
        Guid VariantGroupId = default);

    private sealed record CategoryProductSource(
        Guid Id,
        DateTime CreatedAtUtc,
        Guid CategoryId,
        string? NameAr,
        string? NameEn,
        string Name,
        string? StoreAr,
        string? StoreEn,
        string Store,
        decimal SellingPrice,
        decimal? CompareAtPrice,
        string? PackageTypeAr,
        string? PackageTypeEn,
        decimal? MeasurementValue,
        string? MeasurementUnitAr,
        string? MeasurementUnitEn,
        string? MeasurementUnitSymbol,
        string? Unit,
        string? ImageUrl,
        int SalesCount,
        decimal? Rating,
        int ReviewCount,
        Guid VariantGroupId = default);
}
