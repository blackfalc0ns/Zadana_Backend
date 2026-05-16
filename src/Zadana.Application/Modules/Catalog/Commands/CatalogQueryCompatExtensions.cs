using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace Zadana.Application.Modules.Catalog.Commands;

internal static class CatalogQueryCompatExtensions
{
    public static Task<bool> AnyCompatAsync<T>(
        this IQueryable<T> source,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken)
    {
        try
        {
            if (source.Provider is IAsyncQueryProvider)
            {
                return source.AnyAsync(predicate, cancellationToken);
            }
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("IAsyncQueryProvider", StringComparison.Ordinal))
        {
        }

        return Task.FromResult(source.Any(predicate));
    }

    public static Task<T?> FirstOrDefaultCompatAsync<T>(
        this IQueryable<T> source,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken)
    {
        try
        {
            if (source.Provider is IAsyncQueryProvider)
            {
                return source.FirstOrDefaultAsync(predicate, cancellationToken);
            }
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("IAsyncQueryProvider", StringComparison.Ordinal))
        {
        }

        return Task.FromResult(source.FirstOrDefault(predicate));
    }
}
