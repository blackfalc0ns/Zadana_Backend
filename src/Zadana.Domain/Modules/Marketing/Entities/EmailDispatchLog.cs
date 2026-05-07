using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Marketing.Entities;

public class EmailDispatchLog : BaseEntity
{
    public string? RuleKey { get; private set; }
    public string RuleLabel { get; private set; } = null!;
    public string AudienceType { get; private set; } = null!;
    public string Source { get; private set; } = null!;
    public string Status { get; private set; } = null!;
    public string Subject { get; private set; } = null!;
    public string ToRecipientsJson { get; private set; } = null!;
    public string CcRecipientsJson { get; private set; } = null!;
    public string BccRecipientsJson { get; private set; } = null!;
    public string? Provider { get; private set; }
    public string? ProviderMessageId { get; private set; }
    public string? FailureReason { get; private set; }
    public string? EventKey { get; private set; }
    public Guid? TriggeredByUserId { get; private set; }
    public Guid? EntityId { get; private set; }
    public Guid? VendorId { get; private set; }
    public Guid? BranchId { get; private set; }
    public bool IsTestSend { get; private set; }

    private EmailDispatchLog() { }

    public EmailDispatchLog(
        string? ruleKey,
        string ruleLabel,
        string audienceType,
        string source,
        string status,
        string subject,
        string toRecipientsJson,
        string ccRecipientsJson,
        string bccRecipientsJson,
        string? provider,
        string? providerMessageId,
        string? failureReason,
        string? eventKey,
        Guid? triggeredByUserId,
        Guid? entityId,
        Guid? vendorId,
        Guid? branchId,
        bool isTestSend)
    {
        RuleKey = NormalizeOptional(ruleKey);
        RuleLabel = ruleLabel.Trim();
        AudienceType = audienceType.Trim();
        Source = source.Trim().ToLowerInvariant();
        Status = status.Trim().ToLowerInvariant();
        Subject = subject.Trim();
        ToRecipientsJson = toRecipientsJson.Trim();
        CcRecipientsJson = ccRecipientsJson.Trim();
        BccRecipientsJson = bccRecipientsJson.Trim();
        Provider = NormalizeOptional(provider);
        ProviderMessageId = NormalizeOptional(providerMessageId);
        FailureReason = NormalizeOptional(failureReason);
        EventKey = NormalizeOptional(eventKey);
        TriggeredByUserId = triggeredByUserId;
        EntityId = entityId;
        VendorId = vendorId;
        BranchId = branchId;
        IsTestSend = isTestSend;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
