using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Application.Modules.Identity.DTOs;

public record RoleDefinitionDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsSystem,
    bool IsActive,
    UserRole IdentityRole,
    PanelScope PanelScope,
    List<string> Permissions,
    int UsersCount = 0
);

public record PermissionDefinitionDto(
    Guid Id,
    string Key,
    string Name,
    string Domain,
    string Action,
    PanelScope PanelScope,
    string? Description,
    bool IsSensitive
);
