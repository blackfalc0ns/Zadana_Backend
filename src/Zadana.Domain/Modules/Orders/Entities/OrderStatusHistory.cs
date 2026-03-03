using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Orders.Enums;

namespace Zadana.Domain.Modules.Orders.Entities;

public class OrderStatusHistory
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid OrderId { get; private set; }
    public OrderStatus? OldStatus { get; private set; }
    public OrderStatus NewStatus { get; private set; }
    public Guid? ChangedByUserId { get; private set; }
    public string? Note { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    // Navigation
    public Order Order { get; private set; } = null!;
    public User? ChangedByUser { get; private set; }

    private OrderStatusHistory() { }

    internal OrderStatusHistory(
        Guid orderId, 
        OrderStatus newStatus, 
        Guid? changedByUserId = null, 
        string? note = null, 
        OrderStatus? oldStatus = null)
    {
        OrderId = orderId;
        NewStatus = newStatus;
        ChangedByUserId = changedByUserId;
        Note = note?.Trim();
        OldStatus = oldStatus;
        CreatedAtUtc = DateTime.UtcNow;
    }
}
