using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Identity.Commands.UpdateUserOverrides;

public record UpdateUserOverridesCommand(
    Guid UserId,
    List<string> GrantedPermissions,
    List<string> RevokedPermissions
) : IRequest;

public class UpdateUserOverridesCommandHandler : IRequestHandler<UpdateUserOverridesCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdateUserOverridesCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(UpdateUserOverridesCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FindAsync([request.UserId], cancellationToken);
        if (user is null)
            throw new NotFoundException(nameof(User), request.UserId);

        // Remove all existing overrides for this user
        var existingOverrides = await _context.UserPermissionOverrides
            .Where(o => o.UserId == request.UserId)
            .ToListAsync(cancellationToken);

        _context.UserPermissionOverrides.RemoveRange(existingOverrides);

        // Add granted overrides
        foreach (var key in request.GrantedPermissions)
        {
            _context.UserPermissionOverrides.Add(
                new UserPermissionOverride(request.UserId, key, PermissionOverrideMode.Grant));
        }

        // Add revoked overrides
        foreach (var key in request.RevokedPermissions)
        {
            _context.UserPermissionOverrides.Add(
                new UserPermissionOverride(request.UserId, key, PermissionOverrideMode.Revoke));
        }

        user.IncrementPermissionVersion();
        await _context.SaveChangesAsync(cancellationToken);
    }
}
