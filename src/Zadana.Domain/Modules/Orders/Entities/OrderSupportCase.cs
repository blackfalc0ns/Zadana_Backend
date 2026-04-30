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
    public string? CostBearer { get; private set; }
    public DateTime? ClosedAtUtc { get; private set; }

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
        decimal? requestedRefundAmount = null)
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

        AddActivity(
            "submitted",
            type == OrderSupportCaseType.ReturnRequest ? "Return request submitted" : "Complaint submitted",
            Message,
            customerUserId,
            "customer",
            visibleToCustomer: true);
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
            visibleToCustomer: false);
    }

    public void RequestCustomerEvidence(Guid actorUserId, string? note, string? customerVisibleNote, DateTime? slaDueAtUtc = null)
    {
        EnsureNotClosed("CASE_EVIDENCE_REQUEST_NOT_ALLOWED");

        Status = OrderSupportCaseStatus.AwaitingCustomerEvidence;
        DecisionNotes = string.IsNullOrWhiteSpace(note) ? DecisionNotes : note.Trim();
        CustomerVisibleNote = string.IsNullOrWhiteSpace(customerVisibleNote) ? CustomerVisibleNote : customerVisibleNote.Trim();
        SlaDueAtUtc = slaDueAtUtc ?? SlaDueAtUtc;

        AddActivity(
            "request_evidence",
            "Additional evidence requested",
            customerVisibleNote ?? note,
            actorUserId,
            "admin",
            visibleToCustomer: true);
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
            visibleToCustomer: !string.IsNullOrWhiteSpace(customerVisibleNote));
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
            visibleToCustomer: false);
    }

    public void Approve(
        Guid actorUserId,
        decimal? approvedRefundAmount,
        string? refundMethod,
        string? costBearer,
        string? decisionNotes,
        string? customerVisibleNote)
    {
        EnsureNotClosed("CASE_APPROVAL_NOT_ALLOWED");

        Status = OrderSupportCaseStatus.Approved;
        ApprovedRefundAmount = NormalizeAmount(approvedRefundAmount);
        RefundMethod = NormalizeText(refundMethod);
        CostBearer = NormalizeText(costBearer);
        DecisionNotes = NormalizeText(decisionNotes);
        CustomerVisibleNote = NormalizeText(customerVisibleNote);

        AddActivity(
            "approved",
            "Case approved",
            customerVisibleNote ?? decisionNotes,
            actorUserId,
            "admin",
            visibleToCustomer: true);
    }

    public void Reject(Guid actorUserId, string? decisionNotes, string? customerVisibleNote)
    {
        EnsureNotClosed("CASE_REJECTION_NOT_ALLOWED");

        Status = OrderSupportCaseStatus.Rejected;
        DecisionNotes = NormalizeText(decisionNotes);
        CustomerVisibleNote = NormalizeText(customerVisibleNote);
        ClosedAtUtc = DateTime.UtcNow;

        AddActivity(
            "rejected",
            "Case rejected",
            customerVisibleNote ?? decisionNotes,
            actorUserId,
            "admin",
            visibleToCustomer: true);
    }

    public void Resolve(Guid actorUserId, string? note)
    {
        if (Status == OrderSupportCaseStatus.Resolved)
        {
            return;
        }

        Status = OrderSupportCaseStatus.Resolved;
        ClosedAtUtc = DateTime.UtcNow;

        AddActivity(
            "resolved",
            "Case resolved",
            note,
            actorUserId,
            "admin",
            visibleToCustomer: true);
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
            visibleToCustomer);
    }

    public void AddAttachment(string fileName, string fileUrl, Guid? uploadedByUserId = null)
    {
        Attachments.Add(new OrderSupportCaseAttachment(Id, fileName, fileUrl, uploadedByUserId));
    }

    public void AddCustomerReply(Guid actorUserId, string note, IReadOnlyList<(string FileName, string FileUrl)>? attachments = null)
    {
        if (Status != OrderSupportCaseStatus.AwaitingCustomerEvidence)
        {
            throw new BusinessRuleException("CASE_REPLY_NOT_ALLOWED", "Customer evidence can only be added while the case is awaiting evidence.");
        }

        Status = OrderSupportCaseStatus.InReview;
        CustomerVisibleNote = note.Trim();

        foreach (var attachment in attachments ?? [])
        {
            AddAttachment(attachment.FileName, attachment.FileUrl, actorUserId);
        }

        AddActivity(
            "customer_reply",
            "Customer submitted additional evidence",
            note,
            actorUserId,
            "customer",
            visibleToCustomer: true);
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
        bool visibleToCustomer)
    {
        Activities.Add(new OrderSupportCaseActivity(
            Id,
            action,
            title,
            note,
            actorUserId,
            actorRole,
            visibleToCustomer));
    }

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
