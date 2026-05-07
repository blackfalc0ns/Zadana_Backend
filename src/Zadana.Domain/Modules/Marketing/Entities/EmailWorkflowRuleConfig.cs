using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Marketing.Entities;

public class EmailWorkflowRuleConfig : BaseEntity
{
    public string RuleKey { get; private set; } = null!;
    public string TitleKey { get; private set; } = null!;
    public string SubtitleKey { get; private set; } = null!;
    public string CategoryKey { get; private set; } = null!;
    public string CadenceLabelKey { get; private set; } = null!;
    public string TriggerNotesKey { get; private set; } = null!;
    public bool Enabled { get; private set; }
    public string SenderProfileKey { get; private set; } = null!;
    public string AudienceType { get; private set; } = null!;
    public string PanelScope { get; private set; } = null!;
    public string PersonaTargetsJson { get; private set; } = null!;
    public string EntityScopeJson { get; private set; } = null!;
    public string BranchScopeMode { get; private set; } = null!;
    public string RecipientTargetsJson { get; private set; } = null!;
    public string RouteJson { get; private set; } = null!;
    public string TemplateJson { get; private set; } = null!;
    public string AutomationState { get; private set; } = null!;
    public string? EventKey { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }

    private EmailWorkflowRuleConfig() { }

    public EmailWorkflowRuleConfig(
        string ruleKey,
        string titleKey,
        string subtitleKey,
        string categoryKey,
        string cadenceLabelKey,
        string triggerNotesKey,
        bool enabled,
        string senderProfileKey,
        string audienceType,
        string panelScope,
        string personaTargetsJson,
        string entityScopeJson,
        string branchScopeMode,
        string recipientTargetsJson,
        string routeJson,
        string templateJson,
        string automationState,
        string? eventKey = null,
        Guid? updatedByUserId = null)
    {
        RuleKey = ruleKey.Trim();
        TitleKey = titleKey.Trim();
        SubtitleKey = subtitleKey.Trim();
        CategoryKey = categoryKey.Trim();
        CadenceLabelKey = cadenceLabelKey.Trim();
        TriggerNotesKey = triggerNotesKey.Trim();
        Enabled = enabled;
        SenderProfileKey = senderProfileKey.Trim();
        AudienceType = audienceType.Trim();
        PanelScope = panelScope.Trim();
        PersonaTargetsJson = personaTargetsJson.Trim();
        EntityScopeJson = entityScopeJson.Trim();
        BranchScopeMode = branchScopeMode.Trim();
        RecipientTargetsJson = recipientTargetsJson.Trim();
        RouteJson = routeJson.Trim();
        TemplateJson = templateJson.Trim();
        AutomationState = automationState.Trim().ToLowerInvariant();
        EventKey = NormalizeOptional(eventKey);
        UpdatedByUserId = updatedByUserId;
    }

    public void Update(
        string titleKey,
        string subtitleKey,
        string categoryKey,
        string cadenceLabelKey,
        string triggerNotesKey,
        bool enabled,
        string senderProfileKey,
        string audienceType,
        string panelScope,
        string personaTargetsJson,
        string entityScopeJson,
        string branchScopeMode,
        string recipientTargetsJson,
        string routeJson,
        string templateJson,
        string automationState,
        string? eventKey,
        Guid? updatedByUserId)
    {
        TitleKey = titleKey.Trim();
        SubtitleKey = subtitleKey.Trim();
        CategoryKey = categoryKey.Trim();
        CadenceLabelKey = cadenceLabelKey.Trim();
        TriggerNotesKey = triggerNotesKey.Trim();
        Enabled = enabled;
        SenderProfileKey = senderProfileKey.Trim();
        AudienceType = audienceType.Trim();
        PanelScope = panelScope.Trim();
        PersonaTargetsJson = personaTargetsJson.Trim();
        EntityScopeJson = entityScopeJson.Trim();
        BranchScopeMode = branchScopeMode.Trim();
        RecipientTargetsJson = recipientTargetsJson.Trim();
        RouteJson = routeJson.Trim();
        TemplateJson = templateJson.Trim();
        AutomationState = automationState.Trim().ToLowerInvariant();
        EventKey = NormalizeOptional(eventKey);
        UpdatedByUserId = updatedByUserId;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
