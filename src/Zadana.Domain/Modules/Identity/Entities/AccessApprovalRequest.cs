using Zadana.Domain.Modules.Identity.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Domain.Modules.Identity.Entities;

public class AccessApprovalRequest
{
    public Guid Id { get; private set; }
    public Guid RequestedByUserId { get; private set; }
    public Guid? TargetUserId { get; private set; }
    public string Action { get; private set; } = null!;
    public string Summary { get; private set; } = null!;
    public string PayloadHash { get; private set; } = null!;
    public string PayloadJson { get; private set; } = null!;
    public AccessApprovalStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public Guid? DecidedByUserId { get; private set; }
    public DateTime? DecidedAtUtc { get; private set; }
    public string? DecisionNote { get; private set; }
    public DateTime? ConsumedAtUtc { get; private set; }

    private AccessApprovalRequest() { }

    public AccessApprovalRequest(
        Guid requestedByUserId,
        Guid? targetUserId,
        string action,
        string summary,
        string payloadHash,
        string payloadJson)
    {
        Id = Guid.NewGuid();
        RequestedByUserId = requestedByUserId;
        TargetUserId = targetUserId;
        Action = NormalizeRequired(action, 100, nameof(action));
        Summary = NormalizeRequired(summary, 500, nameof(summary));
        PayloadHash = NormalizeRequired(payloadHash, 128, nameof(payloadHash));
        PayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson;
        Status = AccessApprovalStatus.Pending;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void Approve(Guid decidedByUserId, string? note)
    {
        EnsurePending();
        Status = AccessApprovalStatus.Approved;
        DecidedByUserId = decidedByUserId;
        DecidedAtUtc = DateTime.UtcNow;
        DecisionNote = NormalizeOptional(note, 500);
    }

    public void Reject(Guid decidedByUserId, string? note)
    {
        EnsurePending();
        Status = AccessApprovalStatus.Rejected;
        DecidedByUserId = decidedByUserId;
        DecidedAtUtc = DateTime.UtcNow;
        DecisionNote = NormalizeOptional(note, 500);
    }

    public void Consume()
    {
        if (Status != AccessApprovalStatus.Approved || ConsumedAtUtc.HasValue)
        {
            throw new BusinessRuleException("APPROVAL_NOT_USABLE", "The approval request cannot be consumed.");
        }

        ConsumedAtUtc = DateTime.UtcNow;
    }

    private void EnsurePending()
    {
        if (Status != AccessApprovalStatus.Pending)
        {
            throw new BusinessRuleException("APPROVAL_ALREADY_DECIDED", "The approval request has already been decided.");
        }
    }

    private static string NormalizeRequired(string value, int maxLength, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessRuleException("APPROVAL_FIELD_REQUIRED", $"{field} is required.");
        }

        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }
}
