using Zadana.Application.Common.Caching;
using Zadana.Application.Common.Interfaces;

namespace Zadana.Application.Modules.Geography.Support;

internal static class GeographyCacheInvalidator
{
    public static async Task InvalidateRegionsAsync(
        ICacheInvalidator cacheInvalidator,
        CancellationToken cancellationToken)
    {
        foreach (var culture in new[] { "ar", "en" })
        {
            await cacheInvalidator.RemoveAsync(
                AppCacheKeys.Build("geography", "saudi-regions", culture),
                cancellationToken);
        }
    }

    public static async Task InvalidateCitiesAsync(
        ICacheInvalidator cacheInvalidator,
        string regionCode,
        CancellationToken cancellationToken)
    {
        var normalizedRegionCode = regionCode.Trim().ToUpperInvariant();

        foreach (var culture in new[] { "ar", "en" })
        {
            await cacheInvalidator.RemoveAsync(
                AppCacheKeys.Build("geography", "saudi-cities", normalizedRegionCode, culture),
                cancellationToken);
        }
    }
}
