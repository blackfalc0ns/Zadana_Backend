using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Services;
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

        AccessRoleGuard.EnsureRoleMatchesPanelScope(request.IdentityRole, request.PanelScope);

        var assignedUserIds = role.UserAccessScopes
            .Where(scope => scope.IsActive)
            .Select(scope => scope.UserId)
            .Distinct()
            .ToList();

        if (assignedUserIds.Count > 0 &&
            (role.IdentityRole != request.IdentityRole || role.PanelScope != request.PanelScope))
        {
            throw new BadRequestException(
                "ROLE_ASSIGNMENT_SCOPE_LOCKED",
                "Identity role and panel scope cannot be changed while the role is assigned to active users.");
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
        var invalidPermissions = request.Permissions
            .Except(permissionDefs.Select(p => p.Key), StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (invalidPermissions.Count > 0)
        {
            throw new BadRequestException("INVALID_PERMISSION", $"Unknown permission keys: {string.Join(", ", invalidPermissions)}");
        }

        var wrongScopePermissions = permissionDefs
            .Where(p => p.PanelScope != request.PanelScope)
            .Select(p => p.Key)
            .ToList();
        if (wrongScopePermissions.Count > 0)
        {
            throw new BadRequestException(
                "INVALID_PERMISSION_SCOPE",
                $"Permissions do not belong to the selected panel scope: {string.Join(", ", wrongScopePermissions)}");
        }

        foreach (var permissionDef in permissionDefs)
        {
            role.RolePermissions.Add(new RolePermission(role.Id, permissionDef.Id));
        }

        if (assignedUserIds.Count > 0)
        {
            var users = await _context.Users
                .Where(user => assignedUserIds.Contains(user.Id))
                .ToListAsync(cancellationToken);
            foreach (var user in users)
            {
                user.IncrementPermissionVersion();
            }
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
            Permissions: permissionDefs.Select(p => p.Key).OrderBy(x => x).ToList(),
            UsersCount: role.UserAccessScopes.Count
        );
    }
}
