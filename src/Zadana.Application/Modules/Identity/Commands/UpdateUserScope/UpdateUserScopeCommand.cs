using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Identity.Commands.UpdateUserScope;

public record UpdateUserScopeCommand(
    Guid UserId,
    Guid RoleDefinitionId,
    PanelScope PanelScope,
    AccessScopeType ScopeType,
    Guid? ScopeEntityId,
    string? Notes
) : IRequest;

public class UpdateUserScopeCommandHandler : IRequestHandler<UpdateUserScopeCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdateUserScopeCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(UpdateUserScopeCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FindAsync([request.UserId], cancellationToken);
        if (user is null)
            throw new NotFoundException(nameof(User), request.UserId);

        var role = await _context.RoleDefinitions.FindAsync([request.RoleDefinitionId], cancellationToken);
        if (role is null)
            throw new NotFoundException(nameof(RoleDefinition), request.RoleDefinitionId);

        var existingScope = await _context.UserAccessScopes
            .FirstOrDefaultAsync(s => s.UserId == request.UserId && s.IsActive, cancellationToken);

        if (existingScope is not null)
        {
            existingScope.Update(
                request.RoleDefinitionId,
                request.PanelScope,
                request.ScopeType,
                request.ScopeEntityId,
                request.Notes);
        }
        else
        {
            var newScope = new UserAccessScope(
                request.UserId,
                request.RoleDefinitionId,
                request.PanelScope,
                request.ScopeType,
                request.ScopeEntityId,
                request.Notes);

            _context.UserAccessScopes.Add(newScope);
        }

        user.IncrementPermissionVersion();
        await _context.SaveChangesAsync(cancellationToken);
    }
}
