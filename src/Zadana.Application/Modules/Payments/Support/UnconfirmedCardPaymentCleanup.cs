using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;

namespace Zadana.Application.Modules.Payments.Support;

internal static class UnconfirmedCardPaymentCleanup
{
    public static async Task DeletePaymentAsync(
        IApplicationDbContext context,
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        var payment = await context.Payments
            .Include(item => item.Refunds)
            .FirstOrDefaultAsync(item => item.Id == paymentId, cancellationToken);

        if (payment is null || payment.Status == PaymentStatus.Paid)
        {
            return;
        }

        if (payment.Refunds.Count > 0)
        {
            context.Refunds.RemoveRange(payment.Refunds);
        }

        context.Payments.Remove(payment);
        await context.SaveChangesAsync(cancellationToken);
    }

    public static async Task DeleteOrderAsync(
        IApplicationDbContext context,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var order = await context.Orders
            .FirstOrDefaultAsync(item => item.Id == orderId, cancellationToken);

        if (order is null || order.PaymentStatus == PaymentStatus.Paid || order.Status != OrderStatus.PendingPayment)
        {
            return;
        }

        var payments = await context.Payments
            .Include(item => item.Refunds)
            .Where(item => item.OrderId == orderId)
            .ToListAsync(cancellationToken);

        if (payments.Any(item => item.Status == PaymentStatus.Paid))
        {
            return;
        }

        var refunds = payments.SelectMany(item => item.Refunds).ToList();
        var statusHistory = await context.OrderStatusHistories.Where(item => item.OrderId == orderId).ToListAsync(cancellationToken);
        var items = await context.OrderItems
            .Include(item => item.VendorProduct)
            .Where(item => item.OrderId == orderId)
            .ToListAsync(cancellationToken);
        RestoreReservedStock(items);

        if (refunds.Count > 0)
        {
            context.Refunds.RemoveRange(refunds);
        }

        if (payments.Count > 0)
        {
            context.Payments.RemoveRange(payments);
        }

        if (statusHistory.Count > 0)
        {
            context.OrderStatusHistories.RemoveRange(statusHistory);
        }

        if (items.Count > 0)
        {
            context.OrderItems.RemoveRange(items);
        }

        context.Orders.Remove(order);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static void RestoreReservedStock(IEnumerable<Domain.Modules.Orders.Entities.OrderItem> items)
    {
        var now = DateTime.UtcNow;
        var groupedItems = items
            .Where(item => item.RequiresStockRestore())
            .GroupBy(item => item.VendorProductId)
            .Select(group => new
            {
                VendorProduct = group.First().VendorProduct,
                RestoreQuantity = group.Sum(item => item.Quantity),
                Items = group.ToList()
            });

        foreach (var group in groupedItems)
        {
            group.VendorProduct.IncreaseStock(group.RestoreQuantity);
            foreach (var item in group.Items)
            {
                item.MarkStockRestored(now);
            }
        }
    }
}
