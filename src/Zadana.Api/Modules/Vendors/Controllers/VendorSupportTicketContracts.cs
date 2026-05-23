using System.Globalization;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Vendors.Enums;

namespace Zadana.Api.Modules.Vendors.Controllers;

public sealed record VendorSupportTicketsListResponse(
    List<VendorSupportTicketResponse> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record VendorSupportTicketResponse(
    Guid Id,
    string Reference,
    VendorSupportLocalizedTextResponse Subject,
    string Category,
    string Priority,
    string Status,
    Guid? OrderId,
    string? OrderNumber,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    decimal FirstResponseHours,
    VendorSupportLocalizedTextResponse Summary,
    string AssignedAgentName,
    VendorSupportLocalizedTextResponse AssignedAgentRole,
    bool AssignedAgentOnline,
    List<VendorSupportTagResponse> Tags,
    List<VendorSupportMessageResponse> Messages,
    string? LinkedRoute);

public sealed record VendorSupportLocalizedTextResponse(string Ar, string En);

public sealed record VendorSupportTagResponse(string Id, string LabelKey, string Tone);

public sealed record VendorSupportMessageResponse(
    Guid Id,
    string Direction,
    string Author,
    VendorSupportLocalizedTextResponse Role,
    VendorSupportLocalizedTextResponse Message,
    DateTime CreatedAt);

public sealed record CreateVendorSupportTicketRequest(
    string? Subject,
    string? Category,
    string? Priority,
    string? Message,
    Guid? OrderId);

public sealed record VendorSupportTicketMessageRequest(string? Message);

public sealed record AdminVendorSupportTicketStatusRequest(string? Status, string? Message);

public sealed record AdminVendorSupportTicketAssignRequest(Guid? AssignedAdminId);

internal static class VendorSupportTicketContractMapper
{
    public static VendorSupportTicketResponse Map(
        VendorSupportTicket ticket,
        bool includeMessages = true)
    {
        var summary = string.IsNullOrWhiteSpace(ticket.LastMessagePreview)
            ? ticket.Subject
            : ticket.LastMessagePreview;

        return new VendorSupportTicketResponse(
            ticket.Id,
            ticket.Reference,
            Text(ticket.Subject),
            NormalizeToken(ticket.Category),
            ToApiPriority(ticket.Priority),
            ToApiStatus(ticket.Status),
            ticket.OrderId,
            ticket.Order?.OrderNumber,
            ticket.CreatedAtUtc,
            ticket.UpdatedAtUtc,
            ResolveFirstResponseHours(ticket),
            Text(summary),
            ticket.AssignedAdminId.HasValue ? "Zadana Support" : "Support Queue",
            Text("Vendor Support Specialist"),
            true,
            BuildTags(ticket),
            includeMessages
                ? ticket.Messages
                    .OrderBy(message => message.CreatedAtUtc)
                    .Select(MapMessage)
                    .ToList()
                : [],
            ticket.OrderId.HasValue ? $"/orders/{ticket.OrderId.Value}" : null);
    }

    public static string ToApiStatus(VendorSupportTicketStatus status) =>
        status switch
        {
            VendorSupportTicketStatus.InProgress => "in_progress",
            VendorSupportTicketStatus.WaitingVendor => "waiting_vendor",
            VendorSupportTicketStatus.Resolved => "resolved",
            _ => "open"
        };

    public static string ToApiPriority(VendorSupportTicketPriority priority) =>
        priority.ToString().ToLowerInvariant();

    public static VendorSupportTicketStatus ParseStatus(string? value)
    {
        var normalized = NormalizeToken(value);
        return normalized switch
        {
            "in_progress" or "inprogress" => VendorSupportTicketStatus.InProgress,
            "waiting_vendor" or "waitingvendor" => VendorSupportTicketStatus.WaitingVendor,
            "resolved" => VendorSupportTicketStatus.Resolved,
            _ => VendorSupportTicketStatus.Open
        };
    }

    public static VendorSupportTicketPriority ParsePriority(string? value)
    {
        var normalized = NormalizeToken(value);
        return normalized switch
        {
            "urgent" or "critical" => VendorSupportTicketPriority.Urgent,
            "high" => VendorSupportTicketPriority.High,
            "low" => VendorSupportTicketPriority.Low,
            _ => VendorSupportTicketPriority.Medium
        };
    }

    public static string NormalizeCategory(string? value)
    {
        var normalized = NormalizeToken(value);
        return normalized switch
        {
            "orders" or "products" or "finance" or "offers" or "staff" or "profile" or "technical" or "general" => normalized,
            _ => "general"
        };
    }

    public static string NormalizeToken(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();

    public static VendorSupportLocalizedTextResponse Text(string value) => new(value, value);

    private static VendorSupportMessageResponse MapMessage(VendorSupportTicketMessage message)
    {
        var isAdmin = string.Equals(message.AuthorRole, "admin", StringComparison.OrdinalIgnoreCase);
        return new VendorSupportMessageResponse(
            message.Id,
            isAdmin ? "support" : "vendor",
            isAdmin ? "Zadana Support" : "Vendor Team",
            isAdmin
                ? Text("Vendor Support Specialist")
                : Text("Store Admin"),
            Text(message.Body),
            message.CreatedAtUtc);
    }

    private static List<VendorSupportTagResponse> BuildTags(VendorSupportTicket ticket)
    {
        var tags = new List<VendorSupportTagResponse>();

        if (ticket.Status == VendorSupportTicketStatus.Open)
        {
            tags.Add(new("new", "SUPPORT_CENTER.TAGS.NEW_CASE", "info"));
        }

        if (ticket.Status == VendorSupportTicketStatus.WaitingVendor)
        {
            tags.Add(new("waiting_vendor", "SUPPORT_CENTER.TAGS.WAITING_VENDOR", "warning"));
        }

        if (ticket.Status == VendorSupportTicketStatus.Resolved)
        {
            tags.Add(new("resolved", "SUPPORT_CENTER.TAGS.RESOLVED", "success"));
        }

        if (ticket.Priority is VendorSupportTicketPriority.High or VendorSupportTicketPriority.Urgent &&
            ticket.Status != VendorSupportTicketStatus.Resolved)
        {
            tags.Add(new("sla", "SUPPORT_CENTER.TAGS.SLA_ACTIVE", "warning"));
        }

        return tags;
    }

    private static decimal ResolveFirstResponseHours(VendorSupportTicket ticket)
    {
        if (!ticket.FirstResponseAtUtc.HasValue)
        {
            return 0m;
        }

        var hours = (decimal)(ticket.FirstResponseAtUtc.Value - ticket.CreatedAtUtc).TotalHours;
        return Math.Max(0m, decimal.Round(hours, 1));
    }
}
