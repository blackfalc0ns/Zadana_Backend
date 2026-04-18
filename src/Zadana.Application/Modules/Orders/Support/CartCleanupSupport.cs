using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Payments.Enums;

namespace Zadana.Application.Modules.Orders.Support;

internal static class CartCleanupSupport
{
    public static async Task ClearStalePaidCheckoutCartIfNeededAsync(
        IApplicationDbContext context,
        Guid userId,
        string? guestId,
        CancellationToken cancellationToken)
    {
        var normalizedGuestId = CartLookup.NormalizeGuestId(guestId);

        var userCart = await context.Carts
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        var guestCart = normalizedGuestId is null
            ? null
            : await context.Carts
                .Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.GuestId == normalizedGuestId, cancellationToken);

        if ((userCart == null || userCart.Items.Count == 0) &&
            (guestCart == null || guestCart.Items.Count == 0))
        {
            return;
        }

        var latestPaidCardPayment = await context.Payments
            .Include(x => x.Order)
            .ThenInclude(order => order.Items)
            .Where(x =>
                x.Order.UserId == userId &&
                x.Method == Domain.Modules.Payments.Enums.PaymentMethodType.Card &&
                x.Status == PaymentStatus.Paid)
            .OrderByDescending(x => x.PaidAtUtc ?? x.UpdatedAtUtc)
            .ThenByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestPaidCardPayment == null)
        {
            return;
        }

        var paidAtUtc = latestPaidCardPayment.PaidAtUtc ?? latestPaidCardPayment.UpdatedAtUtc;
        var matchingCarts = new List<Cart>();

        if (ShouldClearCart(userCart, latestPaidCardPayment.Order, paidAtUtc))
        {
            matchingCarts.Add(userCart!);
        }

        if (ShouldClearCart(guestCart, latestPaidCardPayment.Order, paidAtUtc))
        {
            matchingCarts.Add(guestCart!);
        }

        if (matchingCarts.Count == 0)
        {
            return;
        }

        var cartItems = matchingCarts
            .SelectMany(x => x.Items)
            .GroupBy(x => x.Id)
            .Select(x => x.First())
            .ToList();

        if (cartItems.Count > 0)
        {
            context.CartItems.RemoveRange(cartItems);
        }

        context.Carts.RemoveRange(matchingCarts
            .GroupBy(x => x.Id)
            .Select(x => x.First())
            .ToList());

        await context.SaveChangesAsync(cancellationToken);
    }

    private static bool ShouldClearCart(Cart? cart, Domain.Modules.Orders.Entities.Order order, DateTime paymentConfirmedAtUtc)
    {
        if (cart == null || cart.Items.Count == 0)
        {
            return false;
        }

        if (cart.UpdatedAtUtc > paymentConfirmedAtUtc)
        {
            return false;
        }

        var cartSignature = cart.Items
            .GroupBy(x => x.MasterProductId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));

        var orderSignature = order.Items
            .GroupBy(x => x.MasterProductId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));

        if (cartSignature.Count != orderSignature.Count)
        {
            return false;
        }

        foreach (var pair in cartSignature)
        {
            if (!orderSignature.TryGetValue(pair.Key, out var orderedQuantity) || orderedQuantity != pair.Value)
            {
                return false;
            }
        }

        return true;
    }
}
