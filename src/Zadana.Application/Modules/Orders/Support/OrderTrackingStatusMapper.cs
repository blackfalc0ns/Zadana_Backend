using Zadana.Domain.Modules.Orders.Enums;

namespace Zadana.Application.Modules.Orders.Support;

public static class OrderTrackingStatusMapper
{
    public static string ToCustomerTrackingStatus(OrderStatus status) =>
        ToCustomerTrackingStatus(status, FulfillmentType.Delivery);

    public static string ToCustomerTrackingStatus(OrderStatus status, FulfillmentType fulfillment) =>
        fulfillment == FulfillmentType.Pickup
            ? status switch
            {
                OrderStatus.PendingPayment or OrderStatus.Placed or OrderStatus.PendingVendorAcceptance => "pending",
                OrderStatus.Accepted => "accepted",
                OrderStatus.Preparing => "preparing",
                // Dispatch statuses are invalid for pickup; keep showing ready-for-pickup until healed.
                OrderStatus.ReadyForPickup or
                OrderStatus.DriverAssignmentInProgress or
                OrderStatus.DriverAssigned or
                OrderStatus.PickedUp or
                OrderStatus.OnTheWay => "ready_for_pickup",
                OrderStatus.Delivered => "delivered",
                OrderStatus.Cancelled or OrderStatus.VendorRejected or OrderStatus.DeliveryFailed or OrderStatus.Refunded => "cancelled",
                _ => "cancelled"
            }
            : status switch
            {
                OrderStatus.PendingPayment or OrderStatus.Placed or OrderStatus.PendingVendorAcceptance => "pending",
                OrderStatus.Accepted => "accepted",
                OrderStatus.Preparing or OrderStatus.ReadyForPickup or
                OrderStatus.DriverAssignmentInProgress or OrderStatus.DriverAssigned => "preparing",
                OrderStatus.PickedUp or OrderStatus.OnTheWay => "out_for_delivery",
                OrderStatus.Delivered or OrderStatus.Refunded => "delivered",
                _ => "cancelled"
            };

    public static string NormalizeCustomerTrackingStatus(string status) =>
        NormalizeCustomerTrackingStatus(status, FulfillmentType.Delivery);

    public static string NormalizeCustomerTrackingStatus(string status, FulfillmentType fulfillment)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "pending";
        }

        if (Enum.TryParse<OrderStatus>(status, ignoreCase: true, out var parsedStatus))
        {
            return ToCustomerTrackingStatus(parsedStatus, fulfillment);
        }

        var normalized = status.Trim().ToLowerInvariant();
        if (fulfillment == FulfillmentType.Pickup)
        {
            return normalized switch
            {
                "pending" => "pending",
                "accepted" => "accepted",
                "processing" or "preparing" => "preparing",
                "ready_for_pickup" or "driver_assignment_in_progress" or "driver_assigned"
                    or "picked_up" or "on_the_way" or "out_for_delivery" => "ready_for_pickup",
                "delivered" or "returning" => "delivered",
                "refunded" or "cancelled" or "canceled" or "vendor_rejected" or "delivery_failed" => "cancelled",
                _ => normalized
            };
        }

        return normalized switch
        {
            "pending" => "pending",
            "accepted" => "accepted",
            "processing" => "preparing",
            "preparing" => "preparing",
            "ready_for_pickup" => "ready_for_pickup",
            "driver_assignment_in_progress" => "preparing",
            "driver_assigned" => "preparing",
            "picked_up" => "out_for_delivery",
            "on_the_way" => "out_for_delivery",
            "out_for_delivery" => "out_for_delivery",
            "delivered" => "delivered",
            "returning" => "delivered",
            "refunded" => "delivered",
            "cancelled" => "cancelled",
            "canceled" => "cancelled",
            "vendor_rejected" => "cancelled",
            "delivery_failed" => "cancelled",
            _ => normalized
        };
    }
}
