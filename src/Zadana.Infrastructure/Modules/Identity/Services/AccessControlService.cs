using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Infrastructure.Modules.Identity.Services;

public class AccessControlService : IAccessControlService
{
    private readonly IApplicationDbContext _context;

    public AccessControlService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<EffectiveAccessDto> GetEffectiveAccessAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null)
        {
            return new EffectiveAccessDto(0, []);
        }

        var activeScope = await _context.UserAccessScopes
            .AsNoTracking()
            .Include(x => x.RoleDefinition)
            .ThenInclude(x => x.RolePermissions)
            .ThenInclude(x => x.PermissionDefinition)
            .FirstOrDefaultAsync(x => x.UserId == userId && x.IsActive, cancellationToken);

        var permissions = activeScope?.RoleDefinition.RolePermissions
            .Select(x => x.PermissionDefinition.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

        var overrides = await _context.UserPermissionOverrides
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var overrideEntry in overrides)
        {
            if (overrideEntry.Mode == PermissionOverrideMode.Grant)
            {
                permissions.Add(overrideEntry.PermissionKey);
                continue;
            }

            permissions.Remove(overrideEntry.PermissionKey);
        }

        var scopeDto = activeScope is null
            ? null
            : new AccessScopeDto(
                activeScope.PanelScope.ToString(),
                activeScope.ScopeType.ToString(),
                activeScope.ScopeEntityId,
                activeScope.RoleDefinition.Code,
                activeScope.RoleDefinition.Name);

        return new EffectiveAccessDto(
            user.PermissionVersion,
            permissions.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
            scopeDto);
    }
}
