using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Orders.Entities;

namespace Zadana.Application.Modules.Orders.Support;

internal static class OrderStatusHistoryTracking
{
    public static void TrackNewEntries(IApplicationDbContext context, Order order)
    {
        if (order.StatusHistory.Count == 0)
        {
            return;
        }

        if (context is DbContext dbContext)
        {
            var detachedEntries = order.StatusHistory
                .Where(history => dbContext.Entry(history).State == EntityState.Detached)
                .ToList();

            if (detachedEntries.Count > 0)
            {
                context.OrderStatusHistories.AddRange(detachedEntries);
            }

            return;
        }

        context.OrderStatusHistories.AddRange(order.StatusHistory);
    }
}
