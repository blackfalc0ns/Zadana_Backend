using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.DTOs;

namespace Zadana.Application.Modules.Identity.Queries.GetRoles;

public record GetRolesQuery : IRequest<List<RoleDefinitionDto>>;

public class GetRolesQueryHandler : IRequestHandler<GetRolesQuery, List<RoleDefinitionDto>>
{
    private readonly IApplicationDbContext _context;

    public GetRolesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<RoleDefinitionDto>> Handle(GetRolesQuery request, CancellationToken cancellationToken)
    {
        var roles = await _context.RoleDefinitions
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.PermissionDefinition)
            .Include(r => r.UserAccessScopes)
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);

        return roles.Select(r => new RoleDefinitionDto(
            Id: r.Id,
            Code: r.Code,
            Name: r.Name,
            Description: r.Description,
            IsSystem: r.IsSystem,
            IsActive: r.IsActive,
            IdentityRole: r.IdentityRole,
            PanelScope: r.PanelScope,
            Permissions: r.RolePermissions.Select(rp => rp.PermissionDefinition.Key).ToList(),
            UsersCount: r.UserAccessScopes.Count
        )).ToList();
    }
}
