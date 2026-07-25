using Zadana.Domain.Modules.Orders.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Orders.Entities;

public class OrderCancellationRequest : BaseEntity
{
    public Guid OrderId { get; private set; }
    public Guid RequestedByUserId { get; private set; }
    public OrderCancellationRequestStatus Status { get; private set; }
    public string? CustomerReason { get; private set; }
    public string? VendorResponseNote { get; private set; }
    public Guid? DecidedByUserId { get; private set; }
    public DateTime? DecidedAtUtc { get; private set; }

    public Order Order { get; private set; } = null!;

    private OrderCancellationRequest()
    {
    }

    public OrderCancellationRequest(Guid orderId, Guid requestedByUserId, string? customerReason)
    {
        OrderId = orderId;
        RequestedByUserId = requestedByUserId;
        CustomerReason = string.IsNullOrWhiteSpace(customerReason) ? null : customerReason.Trim();
        Status = OrderCancellationRequestStatus.Pending;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Accept(Guid decidedByUserId, string? note = null)
    {
        EnsurePending();
        Status = OrderCancellationRequestStatus.Accepted;
        DecidedByUserId = decidedByUserId;
        DecidedAtUtc = DateTime.UtcNow;
        VendorResponseNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Reject(Guid decidedByUserId, string? note = null)
    {
        EnsurePending();
        Status = OrderCancellationRequestStatus.Rejected;
        DecidedByUserId = decidedByUserId;
        DecidedAtUtc = DateTime.UtcNow;
        VendorResponseNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private void EnsurePending()
    {
        if (Status != OrderCancellationRequestStatus.Pending)
        {
            throw new BusinessRuleException(
                "CANCELLATION_REQUEST_ALREADY_DECIDED",
                "Cancellation request has already been decided.");
        }
    }
}
