using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Infrastructure.Persistence;

namespace Zadana.Infrastructure.Modules.Orders.Services;

internal static class AdminOrderQueryFilters
{
    public static IQueryable<Order> ApplyStatusFilter(IQueryable<Order> query, string status)
    {
        var statuses = ResolveAdminOrderStatuses(status.Trim().ToUpperInvariant());
        return statuses.Length == 0
            ? query
            : query.Where(order => statuses.Contains(order.Status));
    }

    public static IQueryable<Order> ApplyPaymentStatusFilter(
        IQueryable<Order> query,
        ApplicationDbContext db,
        string paymentStatus)
    {
        return paymentStatus.Trim().ToUpperInvariant() switch
        {
            "PENDING" => query.Where(order =>
                (order.PaymentStatus == PaymentStatus.Pending || order.PaymentStatus == PaymentStatus.Initiated) &&
                !db.Refunds.Any(refund => refund.Payment.OrderId == order.Id && refund.Amount > 0)),
            "PAID" => query.Where(order =>
                order.PaymentStatus == PaymentStatus.Paid &&
                !db.Refunds.Any(refund => refund.Payment.OrderId == order.Id && refund.Amount > 0)),
            "FAILED" => query.Where(order => order.PaymentStatus == PaymentStatus.Failed),
            "REFUNDED" => query.Where(order =>
                order.PaymentStatus == PaymentStatus.Refunded ||
                db.Refunds.Any(refund =>
                    refund.Payment.OrderId == order.Id &&
                    refund.Amount > 0 &&
                    order.PaymentStatus == PaymentStatus.Refunded)),
            "PARTIALLY_REFUNDED" => query.Where(order =>
                order.PaymentStatus == PaymentStatus.PartiallyRefunded ||
                db.Refunds.Any(refund =>
                    refund.Payment.OrderId == order.Id &&
                    refund.Amount > 0 &&
                    order.PaymentStatus != PaymentStatus.Refunded)),
            "COD_PENDING" => query.Where(order =>
                (order.PaymentStatus == PaymentStatus.PendingCollection ||
                 order.PaymentStatus == PaymentStatus.Collected ||
                 order.PaymentStatus == PaymentStatus.Cancelled ||
                 order.PaymentStatus == PaymentStatus.PartiallyRefunded) &&
                !db.Refunds.Any(refund => refund.Payment.OrderId == order.Id && refund.Amount > 0)),
            "SETTLED" => query.Where(order => order.PaymentStatus == PaymentStatus.Settled),
            _ => query
        };
    }

    public static IQueryable<Order> ApplyFulfillmentStatusFilter(
        IQueryable<Order> query,
        ApplicationDbContext db,
        string fulfillmentStatus)
    {
        return fulfillmentStatus.Trim().ToUpperInvariant() switch
        {
            "QUEUED" => query.Where(order =>
                order.Status == OrderStatus.PendingPayment ||
                order.Status == OrderStatus.Placed ||
                order.Status == OrderStatus.PendingVendorAcceptance ||
                order.Status == OrderStatus.Accepted),
            "PREPARING" => query.Where(order => order.Status == OrderStatus.Preparing),
            "READY_FOR_PICKUP" => query.Where(order => order.Status == OrderStatus.ReadyForPickup),
            "DRIVER_ASSIGNED" => query.Where(order =>
                order.Status == OrderStatus.DriverAssignmentInProgress ||
                order.Status == OrderStatus.DriverAssigned),
            "PICKED_UP" => query.Where(order => order.Status == OrderStatus.PickedUp),
            "ON_ROUTE" => query.Where(order => order.Status == OrderStatus.OnTheWay),
            "DELIVERED" => query.Where(order =>
                order.Status == OrderStatus.Delivered ||
                order.Status == OrderStatus.Refunded),
            "FAILED" => query.Where(order =>
                order.Status == OrderStatus.DeliveryFailed ||
                db.DeliveryAssignments.Any(assignment =>
                    assignment.OrderId == order.Id &&
                    assignment.Status == AssignmentStatus.Failed)),
            "CANCELLED" => query.Where(order =>
                (order.Status == OrderStatus.Cancelled ||
                 order.Status == OrderStatus.VendorRejected ||
                 order.Status == OrderStatus.PendingBankConfirmation) &&
                !db.DeliveryAssignments.Any(assignment =>
                    assignment.OrderId == order.Id &&
                    assignment.Status == AssignmentStatus.Failed)),
            _ => query
        };
    }

    public static IQueryable<Order> ApplyQueueViewFilter(
        IQueryable<Order> query,
        ApplicationDbContext db,
        string queueView)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-45);

        return queueView.Trim().ToUpperInvariant() switch
        {
            "ACTIVE" => query.Where(order =>
                order.Status != OrderStatus.Cancelled &&
                order.Status != OrderStatus.Delivered &&
                order.Status != OrderStatus.Refunded),
            "LATE" => query.Where(order =>
                order.Status != OrderStatus.Delivered &&
                order.Status != OrderStatus.Cancelled &&
                order.Status != OrderStatus.VendorRejected &&
                order.Status != OrderStatus.DeliveryFailed &&
                order.Status != OrderStatus.Refunded &&
                order.PlacedAtUtc < cutoff),
            "PAYMENT_ISSUES" => query.Where(order =>
                order.PaymentStatus == PaymentStatus.Failed ||
                order.PaymentStatus == PaymentStatus.Pending ||
                order.PaymentStatus == PaymentStatus.Initiated ||
                order.PaymentStatus == PaymentStatus.PendingCollection),
            "REFUNDS" => query.Where(order =>
                order.PaymentStatus == PaymentStatus.Refunded ||
                order.PaymentStatus == PaymentStatus.PartiallyRefunded ||
                db.Refunds.Any(refund => refund.Payment.OrderId == order.Id && refund.Amount > 0) ||
                order.SupportCases.Any(supportCase => supportCase.Type == OrderSupportCaseType.ReturnRequest)),
            _ => query
        };
    }

    private static OrderStatus[] ResolveAdminOrderStatuses(string token) =>
        token switch
        {
            "NEW" =>
            [
                OrderStatus.PendingPayment,
                OrderStatus.Placed,
                OrderStatus.PendingVendorAcceptance
            ],
            "PENDING" => [OrderStatus.Accepted],
            "IN_PROGRESS" =>
            [
                OrderStatus.Preparing,
                OrderStatus.ReadyForPickup,
                OrderStatus.DriverAssignmentInProgress,
                OrderStatus.DriverAssigned
            ],
            "OUT_FOR_DELIVERY" => [OrderStatus.PickedUp, OrderStatus.OnTheWay],
            "DELIVERED" => [OrderStatus.Delivered],
            "COMPLETED" => [OrderStatus.Refunded],
            "CANCELLED" =>
            [
                OrderStatus.Cancelled,
                OrderStatus.VendorRejected,
                OrderStatus.DeliveryFailed,
                OrderStatus.PendingBankConfirmation
            ],
            _ => []
        };
}
