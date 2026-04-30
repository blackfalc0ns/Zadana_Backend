using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Orders.Entities;

public class OrderSupportCaseAttachment : BaseEntity
{
    public Guid OrderSupportCaseId { get; private set; }
    public Guid? UploadedByUserId { get; private set; }
    public string FileName { get; private set; } = null!;
    public string FileUrl { get; private set; } = null!;

    public OrderSupportCase OrderSupportCase { get; private set; } = null!;

    private OrderSupportCaseAttachment()
    {
    }

    public OrderSupportCaseAttachment(Guid orderSupportCaseId, string fileName, string fileUrl, Guid? uploadedByUserId = null)
    {
        OrderSupportCaseId = orderSupportCaseId;
        UploadedByUserId = uploadedByUserId;
        FileName = fileName.Trim();
        FileUrl = fileUrl.Trim();
    }
}
