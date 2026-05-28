using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text;
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
    private readonly IAdminAccessValidationService _validationService;

    public CreateRoleCommandHandler(
        IApplicationDbContext context,
        IAdminAccessValidationService validationService)
    {
        _context = context;
        _validationService = validationService;
    }

    public async Task<RoleDefinitionDto> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        var roleName = NormalizeName(request.Name);
        var description = NormalizeDescription(request.Description);
        var permissions = NormalizePermissions(request.Permissions);
        var code = BuildRoleCode(roleName);
        AccessRoleGuard.EnsureRoleMatchesPanelScope(request.IdentityRole, request.PanelScope);

        if (await _context.RoleDefinitions.AnyAsync(r => r.Code.ToLower() == code.ToLower(), cancellationToken))
        {
            throw new BadRequestException("ROLE_CODE_EXISTS", "A role with this name already exists.");
        }

        var role = new RoleDefinition(
            code: code,
            name: roleName,
            identityRole: request.IdentityRole,
            panelScope: request.PanelScope,
            isSystem: false,
            description: description
        );

        var permissionDefs = await _context.PermissionDefinitions
            .Where(p => permissions.Contains(p.Key))
            .ToListAsync(cancellationToken);
        var invalidPermissions = permissions
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

        await _validationService.EnsureCanManageRoleDefinitionAsync(
            request.IdentityRole,
            request.PanelScope,
            permissions,
            cancellationToken);

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

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BadRequestException("ROLE_NAME_REQUIRED", "Role name is required.");
        }

        var normalized = name.Trim();
        if (normalized.Length > 200)
        {
            throw new BadRequestException("ROLE_NAME_TOO_LONG", "Role name must be 200 characters or fewer.");
        }

        return normalized;
    }

    private static string? NormalizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var normalized = description.Trim();
        if (normalized.Length > 500)
        {
            throw new BadRequestException("ROLE_DESCRIPTION_TOO_LONG", "Role description must be 500 characters or fewer.");
        }

        return normalized;
    }

    private static List<string> NormalizePermissions(IEnumerable<string>? permissions) =>
        (permissions ?? [])
            .Where(permission => !string.IsNullOrWhiteSpace(permission))
            .Select(permission => permission.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string BuildRoleCode(string name)
    {
        var builder = new StringBuilder(name.Length);
        var previousWasSeparator = false;

        foreach (var character in name.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSeparator = false;
                continue;
            }

            if (!previousWasSeparator)
            {
                builder.Append('_');
                previousWasSeparator = true;
            }
        }

        var code = builder.ToString().Trim('_');
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new BadRequestException("ROLE_CODE_INVALID", "Role name must contain at least one letter or digit.");
        }

        return code.Length <= 100 ? code : code[..100].Trim('_');
    }
}
