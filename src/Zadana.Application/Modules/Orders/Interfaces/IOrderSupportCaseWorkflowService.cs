using Zadana.Domain.Modules.Orders.Entities;

namespace Zadana.Application.Modules.Orders.Interfaces;

public interface IOrderSupportCaseWorkflowService
{
    Task<OrderSupportCase> CreateCustomerCaseAsync(
        Guid orderId,
        Guid customerUserId,
        string type,
        string? reasonCode,
        string message,
        IReadOnlyList<OrderSupportCaseAttachmentInput>? attachments,
        CancellationToken cancellationToken = default);

    Task<OrderSupportCase> CreateAdminCaseAsync(
        Guid orderId,
        Guid adminUserId,
        string type,
        string? reasonCode,
        string message,
        string? priority,
        string? queue,
        string? internalNote,
        string? customerVisibleNote,
        CancellationToken cancellationToken = default);

    Task<OrderSupportCase> AssignAsync(
        Guid caseId,
        Guid actorUserId,
        Guid? assignedAdminId,
        string? note,
        string? priority,
        DateTime? slaDueAtUtc,
        CancellationToken cancellationToken = default);

    Task<OrderSupportCase> RequestEvidenceAsync(
        Guid caseId,
        Guid actorUserId,
        string? note,
        string? customerVisibleNote,
        DateTime? slaDueAtUtc,
        CancellationToken cancellationToken = default);

    Task<OrderSupportCase> EscalateAsync(
        Guid caseId,
        Guid actorUserId,
        string? queue,
        string? priority,
        string? note,
        string? customerVisibleNote,
        DateTime? slaDueAtUtc,
        CancellationToken cancellationToken = default);

    Task<OrderSupportCase> ApproveAsync(
        Guid caseId,
        Guid actorUserId,
        decimal? refundAmount,
        string? refundMethod,
        string? costBearer,
        string? decisionNotes,
        string? customerVisibleNote,
        CancellationToken cancellationToken = default);

    Task<OrderSupportCase> RejectAsync(
        Guid caseId,
        Guid actorUserId,
        string? decisionNotes,
        string? customerVisibleNote,
        CancellationToken cancellationToken = default);

    Task<OrderSupportCase> ResolveAsync(
        Guid caseId,
        Guid actorUserId,
        string? note,
        CancellationToken cancellationToken = default);

    Task<OrderSupportCase> ReopenAsync(
        Guid caseId,
        Guid actorUserId,
        string? note,
        CancellationToken cancellationToken = default);

    Task<OrderSupportCase> AddNoteAsync(
        Guid caseId,
        Guid actorUserId,
        string note,
        bool visibleToCustomer,
        CancellationToken cancellationToken = default);
}

public sealed record OrderSupportCaseAttachmentInput(string FileName, string FileUrl);
