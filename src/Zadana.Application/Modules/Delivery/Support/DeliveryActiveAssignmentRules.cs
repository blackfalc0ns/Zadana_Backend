using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;

namespace Zadana.Application.Modules.Delivery.Support;

public static class DeliveryActiveAssignmentRules
{
    public static readonly OrderStatus[] TerminalOrderStatuses =
    [
        OrderStatus.Cancelled,
        OrderStatus.VendorRejected,
        OrderStatus.DeliveryFailed,
        OrderStatus.Refunded,
        OrderStatus.Delivered
    ];

    public static readonly AssignmentStatus[] ActiveMissionAssignmentStatuses =
    [
        AssignmentStatus.Accepted,
        AssignmentStatus.ArrivedAtVendor,
        AssignmentStatus.PickedUp,
        AssignmentStatus.ArrivedAtCustomer
    ];

    public static readonly AssignmentStatus[] OpenAssignmentStatuses =
    [
        AssignmentStatus.OfferSent,
        AssignmentStatus.Accepted,
        AssignmentStatus.ArrivedAtVendor,
        AssignmentStatus.PickedUp,
        AssignmentStatus.ArrivedAtCustomer,
        AssignmentStatus.SearchingDriver
    ];

    public static bool IsTerminalOrder(OrderStatus status) =>
        TerminalOrderStatuses.Contains(status);

    public static bool IsOpenAssignmentStatus(AssignmentStatus status) =>
        OpenAssignmentStatuses.Contains(status);

    public static bool CountsAsActiveMission(DeliveryAssignment assignment, Order order) =>
        assignment.DriverId.HasValue &&
        ActiveMissionAssignmentStatuses.Contains(assignment.Status) &&
        !IsTerminalOrder(order.Status);

    public static bool CountsAsOpenAssignment(DeliveryAssignment assignment, Order order) =>
        assignment.DriverId.HasValue &&
        IsOpenAssignmentStatus(assignment.Status) &&
        !IsTerminalOrder(order.Status);
}
