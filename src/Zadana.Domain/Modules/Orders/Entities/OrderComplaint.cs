using Zadana.Domain.Modules.Orders.Enums;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Orders.Entities;

public class OrderComplaint : BaseEntity
{
    public Guid OrderId { get; private set; }
    public string Message { get; private set; } = null!;
    public OrderComplaintStatus Status { get; private set; }

    public Order Order { get; private set; } = null!;
    public ICollection<OrderComplaintAttachment> Attachments { get; private set; } = [];

    private OrderComplaint() { }

    public OrderComplaint(Guid orderId, string message)
    {
        OrderId = orderId;
        Message = message.Trim();
        Status = OrderComplaintStatus.Submitted;
    }

    public void MarkInReview()
    {
        Status = OrderComplaintStatus.InReview;
    }

    public void Resolve()
    {
        Status = OrderComplaintStatus.Resolved;
    }

    public void AddAttachment(string fileName, string fileUrl)
    {
        Attachments.Add(new OrderComplaintAttachment(Id, fileName, fileUrl));
    }
}
