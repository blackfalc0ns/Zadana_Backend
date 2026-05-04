using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.SharedKernel.Exceptions;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Application.Modules.Identity.Commands.UpdateRole;

public record UpdateRoleCommand(
    Guid Id,
    string Name,
    string? Description,
    UserRole IdentityRole,
    PanelScope PanelScope,
    List<string> Permissions
) : IRequest<RoleDefinitionDto>;

public class UpdateRoleCommandHandler : IRequestHandler<UpdateRoleCommand, RoleDefinitionDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateRoleCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RoleDefinitionDto> Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await _context.RoleDefinitions
            .Include(r => r.RolePermissions)
            .Include(r => r.UserAccessScopes)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (role == null)
        {
            throw new NotFoundException(nameof(RoleDefinition), request.Id);
        }

        if (role.IsSystem)
        {
            throw new BadRequestException("CANNOT_UPDATE_SYSTEM_ROLE", "System roles cannot be modified.");
        }

        role.Update(
            name: request.Name,
            identityRole: request.IdentityRole,
            panelScope: request.PanelScope,
            isSystem: role.IsSystem,
            isActive: role.IsActive,
            description: request.Description
        );

        // Update permissions
        _context.RolePermissions.RemoveRange(role.RolePermissions);
        
        var permissionDefs = await _context.PermissionDefinitions
            .Where(p => request.Permissions.Contains(p.Key))
            .ToListAsync(cancellationToken);

        foreach (var permissionDef in permissionDefs)
        {
            role.RolePermissions.Add(new RolePermission(role.Id, permissionDef.Id));
        }

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
            UsersCount: role.UserAccessScopes.Count
        );
    }
}
