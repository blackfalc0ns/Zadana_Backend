using Zadana.Application.Common.Caching;
using Zadana.Application.Modules.Catalog.DTOs;

namespace Zadana.Application.Modules.Catalog.Queries;

internal static class CatalogQueryCacheKeys
{
    private const string Version = "v3";

    public static string CustomerBrands() =>
        AppCacheKeys.Build("catalog", "brands", "customer", Version, AppCacheKeys.CurrentCulture);

    public static string CategorySubcategories(Guid? categoryId) =>
        AppCacheKeys.Build(
            "catalog",
            "categories",
            "subcategories",
            Version,
            AppCacheKeys.CurrentCulture,
            AppCacheKeys.GuidToken(categoryId));

    public static string CategoryFilters(Guid categoryId) =>
        AppCacheKeys.Build(
            "catalog",
            "categories",
            "filters",
            Version,
            AppCacheKeys.CurrentCulture,
            AppCacheKeys.GuidToken(categoryId));

    public static string BrandFilters(Guid brandId) =>
        AppCacheKeys.Build(
            "catalog",
            "brands",
            "filters",
            Version,
            AppCacheKeys.CurrentCulture,
            AppCacheKeys.GuidToken(brandId));

    public static string SearchProducts(
        string query,
        Guid? categoryId,
        Guid? brandId,
        decimal? minPrice,
        decimal? maxPrice,
        string? sort,
        int page,
        int perPage) =>
        AppCacheKeys.Build(
            "catalog",
            "products",
            "search",
            Version,
            AppCacheKeys.CurrentCulture,
            AppCacheKeys.TextToken(query),
            AppCacheKeys.GuidToken(categoryId),
            AppCacheKeys.GuidToken(brandId),
            AppCacheKeys.DecimalToken(minPrice),
            AppCacheKeys.DecimalToken(maxPrice),
            AppCacheKeys.NormalizeToken(sort),
            AppCacheKeys.IntToken(page),
            AppCacheKeys.IntToken(perPage),
            "public");

    public static string CategoryProducts(
        Guid? categoryId,
        Guid? productTypeId,
        Guid? partId,
        Guid? quantityId,
        Guid? brandId,
        decimal? minPrice,
        decimal? maxPrice,
        string? sort,
        int page,
        int perPage) =>
        AppCacheKeys.Build(
            "catalog",
            "categories",
            "products",
            Version,
            AppCacheKeys.CurrentCulture,
            AppCacheKeys.GuidToken(categoryId),
            AppCacheKeys.GuidToken(productTypeId),
            AppCacheKeys.GuidToken(partId),
            AppCacheKeys.GuidToken(quantityId),
            AppCacheKeys.GuidToken(brandId),
            AppCacheKeys.DecimalToken(minPrice),
            AppCacheKeys.DecimalToken(maxPrice),
            AppCacheKeys.NormalizeToken(sort),
            AppCacheKeys.IntToken(page),
            AppCacheKeys.IntToken(perPage),
            "public");

    public static string BrandProducts(
        Guid brandId,
        Guid? categoryId,
        Guid? subcategoryId,
        Guid? unitId,
        decimal? minPrice,
        decimal? maxPrice,
        string? sort,
        int page,
        int perPage) =>
        AppCacheKeys.Build(
            "catalog",
            "brands",
            "products",
            Version,
            AppCacheKeys.CurrentCulture,
            AppCacheKeys.GuidToken(brandId),
            AppCacheKeys.GuidToken(categoryId),
            AppCacheKeys.GuidToken(subcategoryId),
            AppCacheKeys.GuidToken(unitId),
            AppCacheKeys.DecimalToken(minPrice),
            AppCacheKeys.DecimalToken(maxPrice),
            AppCacheKeys.NormalizeToken(sort),
            AppCacheKeys.IntToken(page),
            AppCacheKeys.IntToken(perPage),
            "public");

    public static string ProductDetails(Guid productId) =>
        AppCacheKeys.Build(
            "catalog",
            "products",
            "details",
            Version,
            AppCacheKeys.CurrentCulture,
            AppCacheKeys.GuidToken(productId),
            "public");
}

internal static class CatalogQueryFavoriteOverlays
{
    public static SearchProductsResponseDto ApplyFavorites(
        SearchProductsResponseDto response,
        IReadOnlySet<Guid> favoriteMasterProductIds) =>
        response with
        {
            Items = response.Items
                .Select(item => item with { IsFavorite = favoriteMasterProductIds.Contains(item.Id) })
                .ToArray()
        };

    public static CategoryProductsDto ApplyFavorites(
        CategoryProductsDto response,
        IReadOnlySet<Guid> favoriteMasterProductIds) =>
        response with
        {
            Items = response.Items
                .Select(item => item with { IsFavorite = favoriteMasterProductIds.Contains(item.Id) })
                .ToArray()
        };

    public static BrandProductsDto ApplyFavorites(
        BrandProductsDto response,
        IReadOnlySet<Guid> favoriteMasterProductIds) =>
        response with
        {
            Items = response.Items
                .Select(item => item with { IsFavorite = favoriteMasterProductIds.Contains(item.Id) })
                .ToArray()
        };

    public static ProductDetailsDto ApplyFavorites(
        ProductDetailsDto response,
        IReadOnlySet<Guid> favoriteMasterProductIds) =>
        response with
        {
            IsFavorite = favoriteMasterProductIds.Contains(response.MasterProductId),
            SimilarProducts = response.SimilarProducts
                .Select(item => item with { IsFavorite = favoriteMasterProductIds.Contains(item.Id) })
                .ToArray()
        };
}
