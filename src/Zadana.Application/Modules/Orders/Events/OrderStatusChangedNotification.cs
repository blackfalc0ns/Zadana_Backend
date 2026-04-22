using MediatR;
using Zadana.Domain.Modules.Orders.Enums;

namespace Zadana.Application.Modules.Orders.Events;

/// <summary>
/// Published after an order's status is changed, triggers real-time notification to customer.
/// </summary>
public record OrderStatusChangedNotification(
    Guid OrderId,
    Guid UserId,
    Guid VendorId,
    string OrderNumber,
    OrderStatus OldStatus,
    OrderStatus NewStatus,
    bool NotifyCustomer = true,
    bool NotifyVendor = false,
    string? ActorRole = null,
    bool CustomerNotificationAlreadySent = false) : INotification;
