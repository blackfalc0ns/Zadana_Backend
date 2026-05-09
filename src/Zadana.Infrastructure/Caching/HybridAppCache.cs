using System.Globalization;
using Microsoft.Extensions.Caching.Hybrid;
using Zadana.Application.Common.Caching;
using Zadana.Application.Common.Interfaces;

namespace Zadana.Infrastructure.Caching;

public sealed class HybridAppCache(HybridCache cache) : IAppCache, ICacheInvalidator
{
    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        AppCacheEntryOptions options,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        var requestCulture = CultureInfo.CurrentCulture;
        var requestUiCulture = CultureInfo.CurrentUICulture;

        return await cache.GetOrCreateAsync(
            key,
            async token =>
            {
                var originalCulture = CultureInfo.CurrentCulture;
                var originalUiCulture = CultureInfo.CurrentUICulture;

                try
                {
                    CultureInfo.CurrentCulture = requestCulture;
                    CultureInfo.CurrentUICulture = requestUiCulture;

                    return await factory(token);
                }
                finally
                {
                    CultureInfo.CurrentCulture = originalCulture;
                    CultureInfo.CurrentUICulture = originalUiCulture;
                }
            },
            CreateEntryOptions(options),
            tags,
            cancellationToken);
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
        cache.RemoveAsync(key, cancellationToken).AsTask();

    public Task RemoveByTagAsync(string tag, CancellationToken cancellationToken = default) =>
        cache.RemoveByTagAsync(tag, cancellationToken).AsTask();

    public async Task RemoveByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default)
    {
        foreach (var tag in tags.Distinct(StringComparer.Ordinal))
        {
            await cache.RemoveByTagAsync(tag, cancellationToken);
        }
    }

    private static HybridCacheEntryOptions CreateEntryOptions(AppCacheEntryOptions options) =>
        new()
        {
            Expiration = options.Expiration,
            LocalCacheExpiration = options.LocalExpiration ?? options.Expiration
        };
}
