using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Services;

namespace Zadana.Application.Modules.Identity.Queries.GetUserEffectiveAccess;

public record GetUserEffectiveAccessQuery(Guid UserId) : IRequest<UserEffectiveAccessDto>;

public record UserEffectiveAccessDto(
    Guid UserId,
    string RoleCode,
    string RoleName,
    List<string> RolePermissions,
    List<string> GrantedOverrides,
    List<string> RevokedOverrides,
    List<string> EffectivePermissions
);

public class GetUserEffectiveAccessQueryHandler : IRequestHandler<GetUserEffectiveAccessQuery, UserEffectiveAccessDto>
{
    private readonly IApplicationDbContext _context;

    public GetUserEffectiveAccessQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<UserEffectiveAccessDto> Handle(GetUserEffectiveAccessQuery request, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FindAsync([request.UserId], cancellationToken);
        if (user is null)
            throw new NotFoundException(nameof(User), request.UserId);

        // Get user's active scope & role
        var scope = await _context.UserAccessScopes
            .Include(s => s.RoleDefinition)
                .ThenInclude(r => r.RolePermissions)
                    .ThenInclude(rp => rp.PermissionDefinition)
            .FirstOrDefaultAsync(s => s.UserId == request.UserId && s.IsActive, cancellationToken);
        var fallbackRole = scope is null
            ? await _context.RoleDefinitions
                .Include(r => r.RolePermissions)
                    .ThenInclude(rp => rp.PermissionDefinition)
                .Where(r => r.IdentityRole == user.Role && r.IsActive)
                .OrderByDescending(r => r.IsSystem)
                .ThenBy(r => r.Code)
                .FirstOrDefaultAsync(r => r.Code == IdentityRoleDefaults.ResolvePreferredRoleCode(user.Role), cancellationToken)
            : null;
        fallbackRole ??= scope is null
            ? await _context.RoleDefinitions
                .Include(r => r.RolePermissions)
                    .ThenInclude(rp => rp.PermissionDefinition)
                .Where(r => r.IdentityRole == user.Role && r.IsActive)
                .OrderByDescending(r => r.IsSystem)
                .ThenBy(r => r.Code)
                .FirstOrDefaultAsync(cancellationToken)
            : null;
        var role = scope?.RoleDefinition ?? fallbackRole;
        var panelScope = scope?.PanelScope ?? role?.PanelScope ?? PanelScope.SuperAdminPanel;

        var rolePermissions = role?.RolePermissions
            .Select(rp => rp.PermissionDefinition.Key)
            .ToList() ?? [];
        var rolePermissionSet = rolePermissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var validOverrideKeys = await _context.PermissionDefinitions
            .AsNoTracking()
            .Where(p => p.PanelScope == panelScope)
            .Select(p => p.Key)
            .ToListAsync(cancellationToken);
        var validOverrideSet = validOverrideKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Get user overrides
        var overrides = await _context.UserPermissionOverrides
            .Where(o => o.UserId == request.UserId && o.IsActive)
            .ToListAsync(cancellationToken);

        var granted = overrides
            .Where(o => o.Mode == PermissionOverrideMode.Grant)
            .Select(o => o.PermissionKey)
            .Where(validOverrideSet.Contains)
            .Where(permission => !rolePermissionSet.Contains(permission))
            .ToList();

        var revoked = overrides
            .Where(o => o.Mode == PermissionOverrideMode.Revoke)
            .Select(o => o.PermissionKey)
            .Where(validOverrideSet.Contains)
            .Where(rolePermissionSet.Contains)
            .ToList();

        // Build effective = (role permissions + granted) - revoked
        var effective = rolePermissions
            .Union(granted)
            .Except(revoked)
            .Distinct()
            .OrderBy(p => p)
            .ToList();

        return new UserEffectiveAccessDto(
            UserId: request.UserId,
            RoleCode: role?.Code ?? IdentityRoleDefaults.ResolvePreferredRoleCode(user.Role),
            RoleName: role?.Name ?? user.Role.ToString(),
            RolePermissions: rolePermissions,
            GrantedOverrides: granted,
            RevokedOverrides: revoked,
            EffectivePermissions: effective
        );
    }
}
