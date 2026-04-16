using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Orders.Entities;

public class OrderComplaintAttachment : BaseEntity
{
    public Guid OrderComplaintId { get; private set; }
    public string FileName { get; private set; } = null!;
    public string FileUrl { get; private set; } = null!;

    public OrderComplaint OrderComplaint { get; private set; } = null!;

    private OrderComplaintAttachment() { }

    public OrderComplaintAttachment(Guid orderComplaintId, string fileName, string fileUrl)
    {
        OrderComplaintId = orderComplaintId;
        FileName = fileName.Trim();
        FileUrl = fileUrl.Trim();
    }
}
