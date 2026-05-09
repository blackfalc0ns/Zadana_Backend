using Zadana.Application.Common.Caching;

namespace Zadana.Application.Common.Interfaces;

public interface IAppCache
{
    Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        AppCacheEntryOptions options,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default);
}
