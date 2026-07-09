using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Vendors.Entities;

public class VendorSupportTicket : BaseEntity
{
    public Guid VendorId { get; private set; }
    public Guid? OrderId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public Guid? AssignedAdminId { get; private set; }
    public DateTime? AssignedAtUtc { get; private set; }
    public string Reference { get; private set; } = null!;
    public string Subject { get; private set; } = null!;
    public string Category { get; private set; } = null!;
    public VendorSupportTicketPriority Priority { get; private set; }
    public VendorSupportTicketStatus Status { get; private set; }
    public string LastMessagePreview { get; private set; } = null!;
    public DateTime? FirstResponseAtUtc { get; private set; }
    public DateTime? ClosedAtUtc { get; private set; }

    public Vendor? Vendor { get; private set; }
    public Order? Order { get; private set; }
    public ICollection<VendorSupportTicketMessage> Messages { get; private set; } = [];

    private VendorSupportTicket()
    {
    }

    public VendorSupportTicket(
        Guid vendorId,
        Guid createdByUserId,
        string reference,
        string subject,
        string category,
        VendorSupportTicketPriority priority,
        string initialMessage,
        Guid? orderId = null)
    {
        VendorId = vendorId;
        CreatedByUserId = createdByUserId;
        Reference = NormalizeRequired(reference, "Reference", 40);
        Subject = NormalizeRequired(subject, "Subject", 300);
        Category = NormalizeRequired(category, "Category", 50).ToLowerInvariant();
        Priority = priority;
        Status = VendorSupportTicketStatus.Open;
        OrderId = orderId;

        AddVendorMessage(createdByUserId, initialMessage);
    }

    public void Assign(Guid adminUserId, Guid? assignedAdminId = null)
    {
        EnsureOpen();
        AssignedAdminId = assignedAdminId ?? adminUserId;
        AssignedAtUtc = DateTime.UtcNow;
        if (Status == VendorSupportTicketStatus.Open)
        {
            Status = VendorSupportTicketStatus.InProgress;
        }
    }

    public void AddVendorMessage(Guid vendorUserId, string message)
    {
        EnsureOpen();
        var normalized = NormalizeRequired(message, "Message", 2000);
        Messages.Add(new VendorSupportTicketMessage(vendorUserId, "vendor", normalized));
        LastMessagePreview = BuildPreview(normalized);
        if (Status == VendorSupportTicketStatus.WaitingVendor)
        {
            Status = VendorSupportTicketStatus.InProgress;
        }
    }

    public void AddAdminMessage(Guid adminUserId, string message)
    {
        EnsureOpen();
        var normalized = NormalizeRequired(message, "Message", 2000);
        Messages.Add(new VendorSupportTicketMessage(adminUserId, "admin", normalized));
        LastMessagePreview = BuildPreview(normalized);
        FirstResponseAtUtc ??= DateTime.UtcNow;
        Status = VendorSupportTicketStatus.WaitingVendor;
    }

    public void SetStatus(VendorSupportTicketStatus status)
    {
        Status = status;
        ClosedAtUtc = status == VendorSupportTicketStatus.Resolved ? DateTime.UtcNow : null;
    }

    private void EnsureOpen()
    {
        if (Status == VendorSupportTicketStatus.Resolved)
        {
            throw new BusinessRuleException("VENDOR_SUPPORT_TICKET_CLOSED", "This support ticket is already closed.");
        }
    }

    private static string NormalizeRequired(string value, string fieldName, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new BusinessRuleException($"INVALID_VENDOR_SUPPORT_{fieldName.ToUpperInvariant()}", $"{fieldName} is required.");
        }

        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string BuildPreview(string message) =>
        message.Length <= 180 ? message : $"{message[..177]}...";
}
