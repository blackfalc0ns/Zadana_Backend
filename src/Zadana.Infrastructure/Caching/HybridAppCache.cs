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
        return await cache.GetOrCreateAsync(
            key,
            async token => await factory(token),
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
