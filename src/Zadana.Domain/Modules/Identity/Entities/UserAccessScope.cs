using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Domain.Modules.Identity.Entities;

public class UserAccessScope
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid RoleDefinitionId { get; private set; }
    public PanelScope PanelScope { get; private set; }
    public AccessScopeType ScopeType { get; private set; }
    public Guid? ScopeEntityId { get; private set; }
    public bool IsActive { get; private set; }
    public string? Notes { get; private set; }
    public DateTime GrantedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public User User { get; private set; } = null!;
    public RoleDefinition RoleDefinition { get; private set; } = null!;

    private UserAccessScope() { }

    public UserAccessScope(
        Guid userId,
        Guid roleDefinitionId,
        PanelScope panelScope,
        AccessScopeType scopeType,
        Guid? scopeEntityId = null,
        string? notes = null)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        RoleDefinitionId = roleDefinitionId;
        PanelScope = panelScope;
        ScopeType = scopeType;
        ScopeEntityId = scopeEntityId;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        IsActive = true;
        GrantedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Update(Guid roleDefinitionId, PanelScope panelScope, AccessScopeType scopeType, Guid? scopeEntityId, string? notes)
    {
        RoleDefinitionId = roleDefinitionId;
        PanelScope = panelScope;
        ScopeType = scopeType;
        ScopeEntityId = scopeEntityId;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
