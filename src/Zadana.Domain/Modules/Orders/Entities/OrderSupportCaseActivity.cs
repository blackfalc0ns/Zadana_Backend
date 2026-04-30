using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Orders.Entities;

public class OrderSupportCaseActivity : BaseEntity
{
    public Guid OrderSupportCaseId { get; private set; }
    public string Action { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public string? Note { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public string ActorRole { get; private set; } = null!;
    public bool VisibleToCustomer { get; private set; }

    public OrderSupportCase OrderSupportCase { get; private set; } = null!;

    private OrderSupportCaseActivity()
    {
    }

    public OrderSupportCaseActivity(
        Guid orderSupportCaseId,
        string action,
        string title,
        string? note,
        Guid? actorUserId,
        string actorRole,
        bool visibleToCustomer)
    {
        OrderSupportCaseId = orderSupportCaseId;
        Action = action.Trim();
        Title = title.Trim();
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        ActorUserId = actorUserId;
        ActorRole = actorRole.Trim();
        VisibleToCustomer = visibleToCustomer;
    }
}
