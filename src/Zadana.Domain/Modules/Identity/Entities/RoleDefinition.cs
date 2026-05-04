using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Domain.Modules.Identity.Entities;

public class RoleDefinition
{
    public Guid Id { get; private set; }
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public bool IsSystem { get; private set; }
    public bool IsActive { get; private set; }
    public UserRole IdentityRole { get; private set; }
    public PanelScope PanelScope { get; private set; }

    public ICollection<RolePermission> RolePermissions { get; private set; } = [];
    public ICollection<UserAccessScope> UserAccessScopes { get; private set; } = [];

    private RoleDefinition() { }

    public RoleDefinition(
        string code,
        string name,
        UserRole identityRole,
        PanelScope panelScope,
        bool isSystem = true,
        string? description = null)
    {
        Id = Guid.NewGuid();
        Code = code.Trim();
        Name = name.Trim();
        IdentityRole = identityRole;
        PanelScope = panelScope;
        IsSystem = isSystem;
        IsActive = true;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    public void Update(
        string name,
        UserRole identityRole,
        PanelScope panelScope,
        bool isSystem,
        bool isActive,
        string? description)
    {
        Name = name.Trim();
        IdentityRole = identityRole;
        PanelScope = panelScope;
        IsSystem = isSystem;
        IsActive = isActive;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }
}
