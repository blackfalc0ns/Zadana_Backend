using Zadana.Domain.Modules.Orders.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Orders.Entities;

public class OrderSupportCase : BaseEntity
{
    public Guid OrderId { get; private set; }
    public Guid CustomerUserId { get; private set; }
    public OrderSupportCaseType Type { get; private set; }
    public OrderSupportCaseStatus Status { get; private set; }
    public OrderSupportCasePriority Priority { get; private set; }
    public OrderSupportCaseQueue Queue { get; private set; }
    public Guid? AssignedAdminId { get; private set; }
    public DateTime? AssignedAtUtc { get; private set; }
    public DateTime? SlaDueAtUtc { get; private set; }
    public string? ReasonCode { get; private set; }
    public string Message { get; private set; } = null!;
    public string? DecisionNotes { get; private set; }
    public string? CustomerVisibleNote { get; private set; }
    public decimal? RequestedRefundAmount { get; private set; }
    public decimal? ApprovedRefundAmount { get; private set; }
    public string? RefundMethod { get; private set; }
    public OrderSupportCaseCompensationType? CompensationType { get; private set; }
    public Guid? CompensationCouponId { get; private set; }
    public string? CostBearer { get; private set; }
    public DateTime? ClosedAtUtc { get; private set; }

    // Multi-stakeholder support
    public string InitiatorRole { get; private set; } = "customer";
    public string? VendorResponse { get; private set; }
    public DateTime? VendorRespondedAtUtc { get; private set; }
    public string? DriverResponse { get; private set; }
    public DateTime? DriverRespondedAtUtc { get; private set; }
    public string? ResolutionCode { get; private set; }
    public string? AwaitingResponseFromRole { get; private set; }

    public Order Order { get; private set; } = null!;
    public ICollection<OrderSupportCaseAttachment> Attachments { get; private set; } = [];
    public ICollection<OrderSupportCaseActivity> Activities { get; private set; } = [];

    private OrderSupportCase()
    {
    }

    public OrderSupportCase(
        Guid orderId,
        Guid customerUserId,
        OrderSupportCaseType type,
        OrderSupportCasePriority priority,
        OrderSupportCaseQueue queue,
        string? reasonCode,
        string message,
        DateTime? slaDueAtUtc = null,
        decimal? requestedRefundAmount = null,
        string initiatorRole = "customer")
    {
        OrderId = orderId;
        CustomerUserId = customerUserId;
        Type = type;
        Status = OrderSupportCaseStatus.Submitted;
        Priority = priority;
        Queue = queue;
        ReasonCode = string.IsNullOrWhiteSpace(reasonCode) ? null : reasonCode.Trim();
        Message = message.Trim();
        SlaDueAtUtc = slaDueAtUtc;
        RequestedRefundAmount = NormalizeAmount(requestedRefundAmount);
        InitiatorRole = string.IsNullOrWhiteSpace(initiatorRole) ? "customer" : initiatorRole.Trim().ToLowerInvariant();

        var submittedTitle = type switch
        {
            OrderSupportCaseType.ReturnRequest => "Return request submitted",
            OrderSupportCaseType.DriverReport => "Driver issue reported",
            OrderSupportCaseType.DriverDispute => "Driver dispute submitted",
            _ => "Complaint submitted"
        };

        AddActivity(
            "submitted",
            submittedTitle,
            Message,
            customerUserId,
            InitiatorRole,
            visibleToCustomer: InitiatorRole == "customer",
            messageType: "case_opened",
            audience: ResolveAudienceForRole(InitiatorRole));
    }

    public bool IsClosed => Status is OrderSupportCaseStatus.Rejected or OrderSupportCaseStatus.Resolved;
    public bool IsActive => Status is not (OrderSupportCaseStatus.Rejected or OrderSupportCaseStatus.Resolved);

    public void Assign(Guid actorUserId, Guid? assignedAdminId, string? note, OrderSupportCasePriority? priority = null, DateTime? slaDueAtUtc = null)
    {
        EnsureNotClosed("CASE_ASSIGN_NOT_ALLOWED");

        AssignedAdminId = assignedAdminId ?? actorUserId;
        AssignedAtUtc = DateTime.UtcNow;
        Priority = priority ?? Priority;
        SlaDueAtUtc = slaDueAtUtc ?? SlaDueAtUtc;
        Status = OrderSupportCaseStatus.InReview;

        AddActivity(
            "assigned",
            "Case assigned for review",
            note,
            actorUserId,
            "admin",
            visibleToCustomer: false,
            messageType: "assignment",
            audience: "internal_admin_only",
            isInternalOnly: true);
    }

    public void RequestCustomerEvidence(Guid actorUserId, string? note, string? customerVisibleNote, DateTime? slaDueAtUtc = null)
    {
        RequestEvidenceFrom(actorUserId, "customer", note, customerVisibleNote, slaDueAtUtc);
    }

    public void RequestEvidenceFrom(Guid actorUserId, string targetRole, string? note, string? publicNote, DateTime? slaDueAtUtc = null)
    {
        EnsureNotClosed("CASE_EVIDENCE_REQUEST_NOT_ALLOWED");

        Status = OrderSupportCaseStatus.AwaitingCustomerEvidence;
        DecisionNotes = string.IsNullOrWhiteSpace(note) ? DecisionNotes : note.Trim();
        CustomerVisibleNote = string.IsNullOrWhiteSpace(publicNote) ? CustomerVisibleNote : publicNote.Trim();
        SlaDueAtUtc = slaDueAtUtc ?? SlaDueAtUtc;
        AwaitingResponseFromRole = NormalizeRole(targetRole) ?? "customer";

        AddActivity(
            "request_evidence",
            $"Additional information requested from {AwaitingResponseFromRole}",
            publicNote ?? note,
            actorUserId,
            "admin",
            visibleToCustomer: string.Equals(AwaitingResponseFromRole, "customer", StringComparison.OrdinalIgnoreCase),
            messageType: "request_evidence",
            audience: ResolveAudienceForTarget(AwaitingResponseFromRole));
    }

    public void Escalate(
        Guid actorUserId,
        OrderSupportCaseQueue queue,
        OrderSupportCasePriority priority,
        string? note,
        string? customerVisibleNote,
        DateTime? slaDueAtUtc = null)
    {
        EnsureNotClosed("CASE_ESCALATION_NOT_ALLOWED");

        Queue = queue;
        Priority = priority;
        SlaDueAtUtc = slaDueAtUtc ?? SlaDueAtUtc;
        AssignedAdminId ??= actorUserId;
        AssignedAtUtc = DateTime.UtcNow;

        if (Status == OrderSupportCaseStatus.Submitted || Status == OrderSupportCaseStatus.AwaitingCustomerEvidence)
        {
            Status = OrderSupportCaseStatus.InReview;
        }

        DecisionNotes = string.IsNullOrWhiteSpace(note) ? DecisionNotes : note.Trim();
        CustomerVisibleNote = string.IsNullOrWhiteSpace(customerVisibleNote) ? CustomerVisibleNote : customerVisibleNote.Trim();

        AddActivity(
            "escalated",
            $"Case escalated to {queue}",
            customerVisibleNote ?? note,
            actorUserId,
            "admin",
            visibleToCustomer: !string.IsNullOrWhiteSpace(customerVisibleNote),
            messageType: "escalation",
            audience: string.IsNullOrWhiteSpace(customerVisibleNote) ? "internal_admin_only" : "customer,vendor",
            isInternalOnly: string.IsNullOrWhiteSpace(customerVisibleNote));
    }

    public void Reopen(Guid actorUserId, string? note)
    {
        Status = OrderSupportCaseStatus.InReview;
        ClosedAtUtc = null;

        AddActivity(
            "reopened",
            "Case reopened",
            note,
            actorUserId,
            "admin",
            visibleToCustomer: false,
            messageType: "reopened",
            audience: "internal_admin_only",
            isInternalOnly: true);
    }

    public void Approve(
        Guid actorUserId,
        decimal? approvedRefundAmount,
        string? refundMethod,
        OrderSupportCaseCompensationType? compensationType,
        Guid? compensationCouponId,
        string? costBearer,
        string? decisionNotes,
        string? customerVisibleNote)
    {
        EnsureNotClosed("CASE_APPROVAL_NOT_ALLOWED");

        Status = OrderSupportCaseStatus.Approved;
        ApprovedRefundAmount = NormalizeAmount(approvedRefundAmount);
        RefundMethod = NormalizeText(refundMethod);
        CompensationType = compensationType;
        CompensationCouponId = compensationCouponId;
        CostBearer = NormalizeText(costBearer);
        DecisionNotes = NormalizeText(decisionNotes);
        CustomerVisibleNote = NormalizeText(customerVisibleNote);
        AwaitingResponseFromRole = null;

        AddActivity(
            "approved",
            "Case approved",
            customerVisibleNote ?? decisionNotes,
            actorUserId,
            "admin",
            visibleToCustomer: true,
            messageType: "decision",
            audience: "all_external");
    }

    public void Reject(Guid actorUserId, string? decisionNotes, string? customerVisibleNote)
    {
        EnsureNotClosed("CASE_REJECTION_NOT_ALLOWED");

        Status = OrderSupportCaseStatus.Rejected;
        DecisionNotes = NormalizeText(decisionNotes);
        CustomerVisibleNote = NormalizeText(customerVisibleNote);
        ClosedAtUtc = DateTime.UtcNow;
        AwaitingResponseFromRole = null;

        AddActivity(
            "rejected",
            "Case rejected",
            customerVisibleNote ?? decisionNotes,
            actorUserId,
            "admin",
            visibleToCustomer: true,
            messageType: "decision",
            audience: "all_external");
    }

    public void Resolve(Guid actorUserId, string? note)
    {
        if (Status == OrderSupportCaseStatus.Resolved)
        {
            return;
        }

        Status = OrderSupportCaseStatus.Resolved;
        ClosedAtUtc = DateTime.UtcNow;
        AwaitingResponseFromRole = null;

        AddActivity(
            "resolved",
            "Case resolved",
            note,
            actorUserId,
            "admin",
            visibleToCustomer: true,
            messageType: "decision",
            audience: "all_external");
    }

    public void AddInternalNote(Guid actorUserId, string note, bool visibleToCustomer)
    {
        EnsureNotClosed("CASE_NOTE_NOT_ALLOWED");

        if (visibleToCustomer)
        {
            CustomerVisibleNote = note.Trim();
        }
        else
        {
            DecisionNotes = note.Trim();
        }

        AddActivity(
            visibleToCustomer ? "customer_note" : "internal_note",
            visibleToCustomer ? "Customer note added" : "Internal note added",
            note,
            actorUserId,
            "admin",
            visibleToCustomer,
            visibleToCustomer ? "public_note" : "internal_note",
            visibleToCustomer ? "customer,vendor" : "internal_admin_only",
            isInternalOnly: !visibleToCustomer);
    }

    public void AddAdminPublicMessage(Guid actorUserId, string message, string audience)
    {
        EnsureNotClosed("CASE_MESSAGE_NOT_ALLOWED");

        if (Status == OrderSupportCaseStatus.Submitted)
        {
            Status = OrderSupportCaseStatus.InReview;
        }

        AddActivity(
            "admin_message",
            "Admin shared an update",
            message,
            actorUserId,
            "admin",
            visibleToCustomer: AudienceIncludes(audience, "customer"),
            messageType: "public_message",
            audience: NormalizeAudienceForStorage(audience));
    }

    public void AddVendorResponse(Guid vendorUserId, string response)
    {
        AddParticipantMessage(vendorUserId, "vendor", response, "customer,vendor");
    }

    public void AddDriverResponse(Guid driverUserId, string response)
    {
        AddParticipantMessage(driverUserId, "driver", response, "driver");
    }

    public void SetResolutionCode(string code)
    {
        ResolutionCode = string.IsNullOrWhiteSpace(code) ? null : code.Trim();
    }

    public void AddAttachment(string fileName, string fileUrl, Guid? uploadedByUserId = null)
    {
        Attachments.Add(new OrderSupportCaseAttachment(Id, fileName, fileUrl, uploadedByUserId));
    }

    public void AddCustomerReply(Guid actorUserId, string note, IReadOnlyList<(string FileName, string FileUrl)>? attachments = null)
    {
        AddParticipantMessage(actorUserId, "customer", note, "customer,vendor", attachments);
    }

    public void MergeIntoActiveCase(
        Guid actorUserId,
        string actorRole,
        string message,
        IReadOnlyList<(string FileName, string FileUrl)>? attachments = null)
    {
        AddParticipantMessage(
            actorUserId,
            actorRole,
            message,
            ResolveAudienceForRole(actorRole),
            attachments,
            messageType: "case_followup");
    }

    private void EnsureNotClosed(string errorCode)
    {
        if (IsClosed)
        {
            throw new BusinessRuleException(errorCode, "This support case is already closed.");
        }
    }

    private void AddActivity(
        string action,
        string title,
        string? note,
        Guid? actorUserId,
        string actorRole,
        bool visibleToCustomer,
        string messageType,
        string audience,
        bool isInternalOnly = false)
    {
        Activities.Add(new OrderSupportCaseActivity(
            Id,
            action,
            title,
            note,
            actorUserId,
            actorRole,
            visibleToCustomer,
            messageType,
            audience,
            isInternalOnly));
    }

    private void AddParticipantMessage(
        Guid actorUserId,
        string actorRole,
        string message,
        string audience,
        IReadOnlyList<(string FileName, string FileUrl)>? attachments = null,
        string messageType = "participant_message")
    {
        EnsureNotClosed($"{actorRole.ToUpperInvariant()}_RESPONSE_NOT_ALLOWED");

        var normalizedRole = NormalizeRole(actorRole)
            ?? throw new BusinessRuleException("INVALID_SUPPORT_CASE_ROLE", "Role is not recognized.");
        var normalizedMessage = message.Trim();

        if (string.IsNullOrWhiteSpace(normalizedMessage))
        {
            throw new BusinessRuleException("EMPTY_SUPPORT_CASE_MESSAGE", "Message is required.");
        }

        if (Status == OrderSupportCaseStatus.AwaitingCustomerEvidence &&
            (string.IsNullOrWhiteSpace(AwaitingResponseFromRole) ||
             string.Equals(AwaitingResponseFromRole, normalizedRole, StringComparison.OrdinalIgnoreCase)))
        {
            Status = OrderSupportCaseStatus.InReview;
            AwaitingResponseFromRole = null;
        }
        else if (Status == OrderSupportCaseStatus.Submitted)
        {
            Status = OrderSupportCaseStatus.InReview;
        }

        foreach (var attachment in attachments ?? [])
        {
            AddAttachment(attachment.FileName, attachment.FileUrl, actorUserId);
        }

        switch (normalizedRole)
        {
            case "customer":
                CustomerVisibleNote = normalizedMessage;
                break;
            case "vendor":
                VendorResponse = normalizedMessage;
                VendorRespondedAtUtc = DateTime.UtcNow;
                break;
            case "driver":
                DriverResponse = normalizedMessage;
                DriverRespondedAtUtc = DateTime.UtcNow;
                break;
        }

        AddActivity(
            $"{normalizedRole}_response",
            $"{ToDisplayRole(normalizedRole)} responded",
            normalizedMessage,
            actorUserId,
            normalizedRole,
            visibleToCustomer: AudienceIncludes(audience, "customer"),
            messageType: messageType,
            audience: NormalizeAudienceForStorage(audience));
    }

    private static string ResolveAudienceForRole(string actorRole) =>
        NormalizeRole(actorRole) switch
        {
            "customer" => "customer,vendor",
            "vendor" => "customer,vendor",
            "driver" => "driver",
            "admin" => "customer,vendor",
            _ => "all_external"
        };

    private static string ResolveAudienceForTarget(string targetRole) =>
        NormalizeRole(targetRole) switch
        {
            "customer" => "customer",
            "vendor" => "vendor",
            "driver" => "driver",
            _ => "customer"
        };

    private static string NormalizeAudienceForStorage(string audience)
    {
        var normalized = audience
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeRole)
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return normalized.Count == 0 ? "all_external" : string.Join(',', normalized);
    }

    private static bool AudienceIncludes(string audience, string role)
    {
        var normalizedAudience = NormalizeAudienceForStorage(audience);
        if (string.Equals(normalizedAudience, "all_external", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return normalizedAudience
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(token => string.Equals(token, role, StringComparison.OrdinalIgnoreCase));
    }

    private static string ToDisplayRole(string role) =>
        NormalizeRole(role) switch
        {
            "customer" => "Customer",
            "vendor" => "Vendor",
            "driver" => "Driver",
            "admin" => "Admin",
            _ => "Participant"
        };

    private static string? NormalizeRole(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    private static string? NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static decimal? NormalizeAmount(decimal? amount)
    {
        if (!amount.HasValue)
        {
            return null;
        }

        if (amount.Value <= 0)
        {
            throw new BusinessRuleException("INVALID_CASE_AMOUNT", "Amount must be greater than zero.");
        }

        return amount.Value;
    }
}
