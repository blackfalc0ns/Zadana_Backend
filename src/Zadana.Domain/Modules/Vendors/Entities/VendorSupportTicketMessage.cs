using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Vendors.Entities;

public class VendorSupportTicketMessage : BaseEntity
{
    public Guid VendorSupportTicketId { get; private set; }
    public Guid? AuthorUserId { get; private set; }
    public string AuthorRole { get; private set; } = null!;
    public string Body { get; private set; } = null!;

    public VendorSupportTicket? VendorSupportTicket { get; private set; }

    private VendorSupportTicketMessage()
    {
    }

    public VendorSupportTicketMessage(
        Guid? authorUserId,
        string authorRole,
        string body)
    {
        AuthorUserId = authorUserId;
        AuthorRole = NormalizeRole(authorRole);
        Body = NormalizeBody(body);
    }

    private static string NormalizeRole(string role)
    {
        var normalized = role?.Trim().ToLowerInvariant();
        if (normalized is not ("vendor" or "admin" or "system"))
        {
            throw new BusinessRuleException("INVALID_VENDOR_SUPPORT_MESSAGE_ROLE", "Message author role is not recognized.");
        }

        return normalized;
    }

    private static string NormalizeBody(string body)
    {
        var normalized = body?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new BusinessRuleException("INVALID_VENDOR_SUPPORT_MESSAGE", "Message is required.");
        }

        return normalized.Length <= 2000 ? normalized : normalized[..2000];
    }
}
