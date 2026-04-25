using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Social.Entities;

public class Review : BaseEntity
{
    public Guid OrderId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid VendorId { get; private set; }
    
    public int Rating { get; private set; }
    public string? Comment { get; private set; }
    public string? VendorReply { get; private set; }
    public DateTime? VendorRepliedAtUtc { get; private set; }
    public DateTime? VendorReplyUpdatedAtUtc { get; private set; }

    // Navigation
    public Order Order { get; private set; } = null!;
    public User User { get; private set; } = null!;
    public Vendor Vendor { get; private set; } = null!;

    private Review() { }

    public Review(Guid orderId, Guid userId, Guid vendorId, int rating, string? comment = null)
    {
        if (rating < 1 || rating > 5) throw new BusinessRuleException("INVALID_RATING", "Rating must be between 1 and 5.");

        OrderId = orderId;
        UserId = userId;
        VendorId = vendorId;
        Rating = rating;
        Comment = comment?.Trim();
    }

    public void SetVendorReply(string reply)
    {
        var normalized = reply.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new BusinessRuleException("INVALID_VENDOR_REPLY", "Reply is required.");
        }

        var now = DateTime.UtcNow;
        VendorReply = normalized;
        VendorReplyUpdatedAtUtc = VendorRepliedAtUtc is null ? null : now;
        VendorRepliedAtUtc ??= now;
    }
}
