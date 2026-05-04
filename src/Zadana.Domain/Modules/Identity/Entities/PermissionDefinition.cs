using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Domain.Modules.Identity.Entities;

public class PermissionDefinition
{
    public Guid Id { get; private set; }
    public string Key { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public string Domain { get; private set; } = null!;
    public string Action { get; private set; } = null!;
    public PanelScope PanelScope { get; private set; }
    public bool IsSensitive { get; private set; }

    public ICollection<RolePermission> RolePermissions { get; private set; } = [];

    private PermissionDefinition() { }

    public PermissionDefinition(
        string key,
        string name,
        string domain,
        string action,
        PanelScope panelScope,
        string? description = null,
        bool isSensitive = false)
    {
        Id = Guid.NewGuid();
        Key = key.Trim();
        Name = name.Trim();
        Domain = domain.Trim();
        Action = action.Trim();
        PanelScope = panelScope;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        IsSensitive = isSensitive;
    }

    public void Update(
        string name,
        string domain,
        string action,
        PanelScope panelScope,
        string? description,
        bool isSensitive)
    {
        Name = name.Trim();
        Domain = domain.Trim();
        Action = action.Trim();
        PanelScope = panelScope;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        IsSensitive = isSensitive;
    }
}
