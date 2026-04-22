using Zadana.Domain.Modules.Orders.Enums;

namespace Zadana.Application.Common.Interfaces;

public interface IOrderStatusNotificationDispatcher
{
    Task<OrderStatusNotificationDispatchResult> DispatchCustomerAsync(
        OrderStatusCustomerNotificationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record OrderStatusCustomerNotificationRequest(
    Guid UserId,
    Guid OrderId,
    Guid VendorId,
    string OrderNumber,
    OrderStatus OldStatus,
    OrderStatus NewStatus,
    string? ActorRole = null);

public sealed record OrderStatusNotificationDispatchResult(
    bool InboxQueued,
    bool RealtimeQueued,
    bool PushAttempted,
    bool PushSent,
    int? PushProviderStatusCode,
    string? PushReason);
