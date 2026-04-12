using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;

namespace Zadana.Application.Modules.Orders.Support;

internal static class CartWriteSupport
{
    public static bool IsRetryableWriteConflict(Exception exception, CartActor actor)
    {
        if (!IsGuestActor(actor) && exception is not DbUpdateConcurrencyException)
        {
            return false;
        }

        if (exception is DbUpdateConcurrencyException)
        {
            return true;
        }

        if (exception is not DbUpdateException dbUpdateException)
        {
            return false;
        }

        var message = dbUpdateException.InnerException?.Message ?? dbUpdateException.Message;
        return message.Contains("IX_Carts_GuestId", StringComparison.OrdinalIgnoreCase)
            || message.Contains("IX_CartItems_CartId_MasterProductId", StringComparison.OrdinalIgnoreCase)
            || message.Contains("GuestId", StringComparison.OrdinalIgnoreCase)
            || message.Contains("CartId", StringComparison.OrdinalIgnoreCase)
            || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
            || message.Contains("unique", StringComparison.OrdinalIgnoreCase);
    }

    public static void ResetTrackedState(IApplicationDbContext context)
    {
        if (context is DbContext dbContext)
        {
            dbContext.ChangeTracker.Clear();
        }
    }

    public static bool IsGuestActor(CartActor actor) =>
        !actor.UserId.HasValue && !string.IsNullOrWhiteSpace(actor.GuestId);
}
