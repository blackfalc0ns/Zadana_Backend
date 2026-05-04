using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Identity.Commands.CreateRole;

public record CreateRoleCommand(
    string Name,
    string? Description,
    UserRole IdentityRole,
    PanelScope PanelScope,
    List<string> Permissions
) : IRequest<RoleDefinitionDto>;

public class CreateRoleCommandHandler : IRequestHandler<CreateRoleCommand, RoleDefinitionDto>
{
    private readonly IApplicationDbContext _context;

    public CreateRoleCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RoleDefinitionDto> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        var code = request.Name.ToLower().Replace(" ", "_");

        var role = new RoleDefinition(
            code: code,
            name: request.Name,
            identityRole: request.IdentityRole,
            panelScope: request.PanelScope,
            isSystem: false,
            description: request.Description
        );

        var permissionDefs = await _context.PermissionDefinitions
            .Where(p => request.Permissions.Contains(p.Key))
            .ToListAsync(cancellationToken);

        foreach (var permissionDef in permissionDefs)
        {
            role.RolePermissions.Add(new RolePermission(role.Id, permissionDef.Id));
        }

        _context.RoleDefinitions.Add(role);
        await _context.SaveChangesAsync(cancellationToken);

        return new RoleDefinitionDto(
            Id: role.Id,
            Code: role.Code,
            Name: role.Name,
            Description: role.Description,
            IsSystem: role.IsSystem,
            IsActive: role.IsActive,
            IdentityRole: role.IdentityRole,
            PanelScope: role.PanelScope,
            Permissions: request.Permissions,
            UsersCount: 0
        );
    }
}
