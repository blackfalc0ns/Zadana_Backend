using Zadana.Domain.Modules.Orders.Enums;

namespace Zadana.Application.Modules.Orders.Support;

public static class OrderTrackingStatusMapper
{
    public static string ToCustomerTrackingStatus(OrderStatus status) =>
        status switch
        {
            OrderStatus.PendingPayment or OrderStatus.Placed or OrderStatus.PendingVendorAcceptance => "pending",
            OrderStatus.Accepted => "accepted",
            OrderStatus.Preparing or OrderStatus.ReadyForPickup or
            OrderStatus.DriverAssignmentInProgress or OrderStatus.DriverAssigned => "preparing",
            OrderStatus.PickedUp or OrderStatus.OnTheWay => "out_for_delivery",
            OrderStatus.Delivered => "delivered",
            OrderStatus.Refunded => "returning",
            _ => "cancelled"
        };

    public static string NormalizeCustomerTrackingStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "pending";
        }

        if (Enum.TryParse<OrderStatus>(status, ignoreCase: true, out var parsedStatus))
        {
            return ToCustomerTrackingStatus(parsedStatus);
        }

        return status.Trim().ToLowerInvariant() switch
        {
            "pending" => "pending",
            "accepted" => "accepted",
            "processing" => "preparing",
            "preparing" => "preparing",
            "ready_for_pickup" => "preparing",
            "driver_assignment_in_progress" => "preparing",
            "driver_assigned" => "preparing",
            "picked_up" => "out_for_delivery",
            "on_the_way" => "out_for_delivery",
            "out_for_delivery" => "out_for_delivery",
            "delivered" => "delivered",
            "returning" => "returning",
            "refunded" => "returning",
            "cancelled" => "cancelled",
            "canceled" => "cancelled",
            "vendor_rejected" => "cancelled",
            "delivery_failed" => "cancelled",
            var normalized => normalized
        };
    }
}
