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
using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.Domain.Modules.Vendors.Enums;

namespace Zadana.Application.Modules.Catalog.Queries.Products.SearchProducts;

public class SearchProductsQueryHandler : IRequestHandler<SearchProductsQuery, SearchProductsResponseDto>
{
    private const int DefaultPage = 1;
    private const int DefaultPerPage = 20;
    private const int MaxPerPage = 100;

    private readonly IApplicationDbContext _context;
    private readonly IAppCache _cache;
    private readonly ICatalogReadCacheService _catalogReadCacheService;
    private readonly CacheDurationSettings _durations;

    public SearchProductsQueryHandler(
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

    public async Task<SearchProductsResponseDto> Handle(SearchProductsQuery request, CancellationToken cancellationToken)
    {
        var normalizedQuery = request.Query.Trim();
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return new SearchProductsResponseDto(string.Empty, 0, NormalizePage(request.Page), NormalizePerPage(request.PerPage), []);
        }

        var page = NormalizePage(request.Page);
        var perPage = NormalizePerPage(request.PerPage);
        var baseResponse = await _cache.GetOrCreateAsync(
            CatalogQueryCacheKeys.SearchProducts(
                normalizedQuery,
                request.CategoryId,
                request.BrandId,
                request.MinPrice,
                request.MaxPrice,
                request.Sort,
                page,
                perPage),
            token => BuildBaseResponseAsync(request, normalizedQuery, page, perPage, token),
            new AppCacheEntryOptions(_durations.BrowseBase),
            [CacheTagNames.Catalog],
            cancellationToken);

        var favoriteMasterProductIds = await _catalogReadCacheService.GetCurrentFavoriteMasterProductIdsAsync(cancellationToken);
        return CatalogQueryFavoriteOverlays.ApplyFavorites(baseResponse, favoriteMasterProductIds);
    }

    private async Task<SearchProductsResponseDto> BuildBaseResponseAsync(
        SearchProductsQuery request,
        string normalizedQuery,
        int page,
        int perPage,
        CancellationToken cancellationToken)
    {
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
                (!request.CategoryId.HasValue || product.MasterProduct.CategoryId == request.CategoryId.Value) &&
                (!request.BrandId.HasValue || product.MasterProduct.BrandId == request.BrandId.Value) &&
                (!request.MinPrice.HasValue || product.SellingPrice >= request.MinPrice.Value) &&
                (!request.MaxPrice.HasValue || product.SellingPrice <= request.MaxPrice.Value) &&
                (
                    product.MasterProduct.NameAr.Contains(normalizedQuery) ||
                    product.MasterProduct.NameEn.Contains(normalizedQuery) ||
                    (!string.IsNullOrWhiteSpace(product.CustomNameAr) && product.CustomNameAr.Contains(normalizedQuery)) ||
                    (!string.IsNullOrWhiteSpace(product.CustomNameEn) && product.CustomNameEn.Contains(normalizedQuery)) ||
                    (product.MasterProduct.DescriptionAr != null && product.MasterProduct.DescriptionAr.Contains(normalizedQuery)) ||
                    (product.MasterProduct.DescriptionEn != null && product.MasterProduct.DescriptionEn.Contains(normalizedQuery)) ||
                    (product.MasterProduct.Barcode != null && product.MasterProduct.Barcode.Contains(normalizedQuery))
                ))
            .Select(product => new RawSearchProduct(
                product.Id,
                product.MasterProductId,
                product.CreatedAtUtc,
                product.VendorId,
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

                return new SearchProductSource(
                    product.MasterProductId,
                    product.CreatedAtUtc,
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
                        MasterProductDisplayDto.BuildLegacyUnit(product.PackageTypeAr, product.MeasurementUnitAr, true),
                        MasterProductDisplayDto.BuildLegacyUnit(product.PackageTypeEn, product.MeasurementUnitEn, false)),
                    product.ImageUrl,
                    salesCount,
                    reviewStats?.AverageRating,
                    reviewStats?.ReviewCount ?? 0,
                    product.VariantGroupId);
            })
            .GroupBy(product => product.VariantGroupId)
            .Select(group => group
                .OrderBy(product => product.SellingPrice)
                .ThenByDescending(product => product.CreatedAtUtc)
                .ThenBy(product => product.Store, StringComparer.CurrentCultureIgnoreCase)
                .First());

        var sortedProducts = ApplySorting(products, request.Sort).ToList();
        var total = sortedProducts.Count;

        // Compute variant counts per variant group
        var variantCountByGroupId = rawProducts
            .GroupBy(p => p.VariantGroupId)
            .ToDictionary(g => g.Key, g => g.Select(p => p.MasterProductId).Distinct().Count());

        var items = sortedProducts
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .Select(product =>
            {
                var variantCount = variantCountByGroupId.GetValueOrDefault(product.VariantGroupId, 1);
                return MapToProductItem(product, false, variantCount);
            })
            .ToList();

        return new SearchProductsResponseDto(normalizedQuery, total, page, perPage, items);
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

    private IEnumerable<SearchProductSource> ApplySorting(IEnumerable<SearchProductSource> products, string? sort)
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

    private SearchProductItemDto MapToProductItem(SearchProductSource product, bool isFavorite, int variantCount = 1)
    {
        var isDiscounted = product.CompareAtPrice.HasValue && product.CompareAtPrice.Value > product.SellingPrice;

        return new SearchProductItemDto(
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
            isDiscounted,
            variantCount);
    }

    private static decimal CalculateDiscountRate(SearchProductSource product)
    {
        if (!product.CompareAtPrice.HasValue || product.CompareAtPrice.Value <= 0 || product.CompareAtPrice.Value <= product.SellingPrice)
        {
            return 0;
        }

        return (product.CompareAtPrice.Value - product.SellingPrice) / product.CompareAtPrice.Value;
    }

    private static string? FormatDiscount(SearchProductSource product)
    {
        var rate = CalculateDiscountRate(product);
        return rate <= 0
            ? null
            : $"{Math.Round(rate * 100, MidpointRounding.AwayFromZero):0}%";
    }

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

    private static string? PickLocalizedNullable(string? arabic, string? english)
    {
        var value = PickLocalized(arabic, english);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record RawSearchProduct(
        Guid Id,
        Guid MasterProductId,
        DateTime CreatedAtUtc,
        Guid VendorId,
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
        Guid VariantGroupId);

    private sealed record SearchProductSource(
        Guid Id,
        DateTime CreatedAtUtc,
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
