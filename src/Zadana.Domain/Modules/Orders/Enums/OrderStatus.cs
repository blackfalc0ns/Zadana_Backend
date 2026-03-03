namespace Zadana.Domain.Modules.Orders.Enums;

public enum OrderStatus
{
    PendingPayment,
    Placed,
    PendingVendorAcceptance,
    VendorRejected,
    Accepted,
    Preparing,
    ReadyForPickup,
    DriverAssignmentInProgress,
    DriverAssigned,
    PickedUp,
    OnTheWay,
    Delivered,
    DeliveryFailed,
    Cancelled,
    Refunded
}
