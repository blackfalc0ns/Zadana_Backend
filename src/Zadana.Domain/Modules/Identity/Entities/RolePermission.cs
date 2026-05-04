namespace Zadana.Domain.Modules.Identity.Entities;

public class RolePermission
{
    public Guid RoleDefinitionId { get; private set; }
    public Guid PermissionDefinitionId { get; private set; }

    public RoleDefinition RoleDefinition { get; private set; } = null!;
    public PermissionDefinition PermissionDefinition { get; private set; } = null!;

    private RolePermission() { }

    public RolePermission(Guid roleDefinitionId, Guid permissionDefinitionId)
    {
        RoleDefinitionId = roleDefinitionId;
        PermissionDefinitionId = permissionDefinitionId;
    }
}
