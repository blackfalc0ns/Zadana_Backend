using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Services;
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
        AccessRoleGuard.EnsureRoleMatchesPanelScope(request.IdentityRole, request.PanelScope);

        if (await _context.RoleDefinitions.AnyAsync(r => r.Code == code, cancellationToken))
        {
            throw new BadRequestException("ROLE_CODE_EXISTS", "A role with this name already exists.");
        }

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
            Permissions: permissionDefs.Select(p => p.Key).OrderBy(x => x).ToList(),
            UsersCount: 0
        );
    }
}
