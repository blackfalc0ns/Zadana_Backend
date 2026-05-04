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
