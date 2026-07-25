using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Orders.Events;
using Zadana.Application.Modules.Orders.Services;
using Zadana.Application.Modules.Payments.Interfaces;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Orders.Support;

public static class BranchActivePickupOrdersSupport
{
    public static IQueryable<Order> ActivePickupOrdersQuery(IQueryable<Order> query, Guid branchId) =>
        query.Where(order =>
            order.VendorBranchId == branchId &&
            order.Fulfillment == FulfillmentType.Pickup &&
            order.Status != OrderStatus.Cancelled &&
            order.Status != OrderStatus.Delivered &&
            order.Status != OrderStatus.Refunded &&
            order.Status != OrderStatus.VendorRejected &&
            order.Status != OrderStatus.DeliveryFailed);

    public static async Task EnsureNoActivePickupOrdersAsync(
        IApplicationDbContext context,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        var hasActiveOrders = await ActivePickupOrdersQuery(context.Orders.AsNoTracking(), branchId)
            .AnyAsync(cancellationToken);

        if (hasActiveOrders)
        {
            throw new BusinessRuleException(
                "BRANCH_HAS_ACTIVE_PICKUP_ORDERS",
                "This branch has active pickup orders. Complete or cancel them before deactivating the branch.");
        }
    }

    public static async Task ForceCancelActivePickupOrdersAsync(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IPublisher publisher,
        IPaymentGatewayResolver gatewayResolver,
        OrderInventoryWorkflowService inventoryWorkflowService,
        ILogger logger,
        Guid branchId,
        string reason,
        CancellationToken cancellationToken)
    {
        var orders = await ActivePickupOrdersQuery(
                context.Orders.Include(order => order.StatusHistory),
                branchId)
            .ToListAsync(cancellationToken);

        if (orders.Count == 0)
        {
            return;
        }

        var notifications = new List<OrderStatusChangedNotification>();

        foreach (var order in orders)
        {
            var oldStatus = order.Status;
            order.ChangeStatus(OrderStatus.Cancelled, null, reason);
            context.OrderStatusHistories.Add(order.StatusHistory.Last());
            await inventoryWorkflowService.ApplyRestockAsync(order.Id, "branch_force_deactivated", cancellationToken);

            await OrderCancellationRefundSupport.TryRefundPaidOrderAsync(
                context,
                gatewayResolver,
                logger,
                order,
                reason,
                cancellationToken);

            notifications.Add(new OrderStatusChangedNotification(
                order.Id,
                order.UserId,
                order.VendorId,
                order.OrderNumber,
                oldStatus,
                OrderStatus.Cancelled,
                NotifyCustomer: true,
                NotifyVendor: true,
                ActorRole: "admin"));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var notification in notifications)
        {
            await publisher.Publish(notification, cancellationToken);
        }
    }
}
