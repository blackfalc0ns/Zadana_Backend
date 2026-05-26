namespace Zadana.Application.Modules.EmailCenter.DTOs;

public record EmailCenterOverviewDto(
    List<EmailSenderProfileDto> SenderProfiles,
    List<EmailWorkflowRuleDto> Rules,
    EmailCenterKpiSnapshotDto Kpi,
    List<EmailScopeOptionDto> Vendors,
    List<EmailBranchOptionDto> Branches);

public record EmailCenterKpiSnapshotDto(
    int TotalRules,
    int EnabledRules,
    int SenderProfiles,
    int DirectoryDrivenRules,
    int AudienceCoverage);

public record EmailSenderProfileDto(
    string Id,
    string Name,
    string Address,
    string ReplyTo,
    string DescriptionKey,
    string Locale,
    bool IsDefault,
    string Status,
    bool IsReadOnly);

public record EmailWorkflowRuleDto(
    string Id,
    string TitleKey,
    string SubtitleKey,
    string CategoryKey,
    string CadenceLabelKey,
    string TriggerNotesKey,
    bool Enabled,
    string SenderProfileId,
    string AudienceType,
    string PanelScope,
    List<string> PersonaTargets,
    EmailEntityScopeDto EntityScope,
    string BranchScopeMode,
    EmailRecipientTargetSelectionDto RecipientTargets,
    EmailRecipientRouteDto Route,
    EmailTemplatePreviewDto Template,
    string AutomationState,
    string? EventKey,
    EmailDispatchSummaryDto? LastDispatch);

public record EmailEntityScopeDto(
    string? EntityId,
    string? VendorId,
    string? BranchId);

public record EmailRecipientTargetSelectionDto(
    List<string> To,
    List<string> Cc,
    List<string> Bcc);

public record EmailRecipientRouteDto(
    List<string> StaticTo,
    List<string> StaticCc,
    List<string> StaticBcc,
    List<string> FallbackTo,
    List<string> FallbackCc,
    List<string> FallbackBcc,
    string Owner,
    string Escalation);

public record EmailTemplatePreviewDto(
    Dictionary<string, string> Subject,
    Dictionary<string, string> Body,
    List<string> Variables,
    string? HeroImageUrl = null,
    string? CtaLabel = null,
    string? HeroImageUrlAr = null,
    string? HeroImageUrlEn = null);

public record EmailDispatchSummaryDto(
    string Status,
    string Source,
    DateTime CreatedAtUtc,
    string? FailureReason);

public record EmailResolvedRecipientsDto(
    List<string> To,
    List<string> Cc,
    List<string> Bcc,
    List<string> Warnings);

public record EmailDispatchLogDto(
    Guid Id,
    string? RuleId,
    string RuleLabel,
    string AudienceType,
    string Source,
    string Status,
    string Subject,
    List<string> To,
    List<string> Cc,
    List<string> Bcc,
    string? Provider,
    string? ProviderMessageId,
    string? FailureReason,
    string? EventKey,
    bool IsTestSend,
    DateTime CreatedAtUtc);

public record EmailTestSendResultDto(
    Guid DispatchId,
    string Status,
    string? Provider,
    string? ProviderMessageId,
    string? FailureReason,
    DateTime CreatedAtUtc);

public record EmailScopeOptionDto(
    string Id,
    string Name);

public record EmailBranchOptionDto(
    string Id,
    string VendorId,
    string Name);

public sealed record EmailDispatchOperationResult(
    bool Attempted,
    bool Sent,
    bool Skipped,
    string Source,
    string? Provider,
    string? ProviderMessageId,
    string? Reason);

public sealed record EmailSystemEventDispatchRequest(
    string EventKey,
    string AudienceType,
    IReadOnlyList<string> To,
    IReadOnlyDictionary<string, string>? Variables = null,
    string? TargetUrl = null,
    Guid? EntityId = null,
    Guid? VendorId = null,
    Guid? BranchId = null,
    IReadOnlyList<string>? Cc = null,
    IReadOnlyList<string>? Bcc = null,
    DateTime? DuplicateWindowStartUtc = null,
    DateTime? DuplicateWindowEndUtc = null,
    string Source = "system_event");
