namespace Zadana.Application.Common.Caching;

internal static class CacheInvalidationProfiles
{
    public static readonly string[] CatalogReadModels =
    [
        CacheTagNames.Catalog,
        CacheTagNames.CatalogFilters,
        CacheTagNames.Home
    ];

    public static readonly string[] HomeReadModels =
    [
        CacheTagNames.Home
    ];
}
