using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Identity.Commands.DeleteRole;

public record DeleteRoleCommand(Guid Id) : IRequest;

public class DeleteRoleCommandHandler : IRequestHandler<DeleteRoleCommand>
{
    private readonly IApplicationDbContext _context;

    public DeleteRoleCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await _context.RoleDefinitions
            .Include(r => r.UserAccessScopes)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (role == null)
        {
            throw new NotFoundException("RoleDefinition", request.Id);
        }

        if (role.IsSystem)
        {
            throw new BadRequestException("CANNOT_DELETE_SYSTEM_ROLE", "System roles cannot be deleted.");
        }

        if (role.UserAccessScopes.Any())
        {
            throw new BadRequestException("ROLE_IN_USE", "Cannot delete a role that is assigned to users.");
        }

        _context.RoleDefinitions.Remove(role);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
