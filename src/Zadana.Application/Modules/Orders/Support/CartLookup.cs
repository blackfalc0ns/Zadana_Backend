using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Orders.Entities;

namespace Zadana.Application.Modules.Orders.Support;

internal static class CartLookup
{
    public static string? NormalizeGuestId(string? guestId) =>
        string.IsNullOrWhiteSpace(guestId) ? null : guestId.Trim();

    public static Task<Cart?> FindCartAsync(
        IApplicationDbContext context,
        CartActor actor,
        CancellationToken cancellationToken,
        bool includeItems = false,
        bool asTracking = true)
    {
        return FindCartAsync(context, actor.UserId, actor.GuestId, cancellationToken, includeItems, asTracking);
    }

    public static Task<Cart?> FindCartAsync(
        IApplicationDbContext context,
        Guid? userId,
        string? guestId,
        CancellationToken cancellationToken,
        bool includeItems = false,
        bool asTracking = true)
    {
        var normalizedGuestId = NormalizeGuestId(guestId);
        IQueryable<Cart> query = asTracking
            ? context.Carts
            : context.Carts.AsNoTracking();

        if (includeItems)
        {
            query = query.Include(item => item.Items);
        }

        query = userId.HasValue
            ? query.Where(item => item.UserId == userId.Value)
            : query.Where(item => item.GuestId == normalizedGuestId);

        return query
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ThenByDescending(item => item.CreatedAtUtc)
            .ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
