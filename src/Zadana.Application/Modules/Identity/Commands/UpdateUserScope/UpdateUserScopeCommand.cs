using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.Services;
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
    private readonly ICurrentUserService _currentUserService;
    private readonly IAdminAccessValidationService _validationService;
    private readonly IAccessAuditService _auditService;

    public UpdateUserScopeCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IAdminAccessValidationService validationService,
        IAccessAuditService auditService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _validationService = validationService;
        _auditService = auditService;
    }

    public async Task Handle(UpdateUserScopeCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FindAsync([request.UserId], cancellationToken);
        if (user is null)
            throw new NotFoundException(nameof(User), request.UserId);

        var role = await _context.RoleDefinitions.FindAsync([request.RoleDefinitionId], cancellationToken);
        if (role is null)
            throw new NotFoundException(nameof(RoleDefinition), request.RoleDefinitionId);

        if (!role.IsActive)
            throw new BadRequestException("ROLE_INACTIVE", "The selected role is inactive.");

        AccessRoleGuard.EnsureRoleCanBeAssignedToUser(user, role);

        await _validationService.EnsureCanMutateUserAccessAsync(
            targetUserId: user.Id,
            targetRole: user.Role,
            requestedStatus: null,
            actorUserId: _currentUserService.UserId,
            newRole: role,
            grantedPermissions: [],
            revokedPermissions: [],
            cancellationToken);

        var normalizedScopeEntityId = await _validationService.NormalizeAndValidateScopeAsync(
            role,
            request.PanelScope,
            request.ScopeType,
            request.ScopeEntityId,
            user.Id,
            cancellationToken);

        var existingScope = await _context.UserAccessScopes
            .FirstOrDefaultAsync(s => s.UserId == request.UserId && s.IsActive, cancellationToken);

        if (existingScope is not null)
        {
            existingScope.Update(
                request.RoleDefinitionId,
                request.PanelScope,
                request.ScopeType,
                normalizedScopeEntityId,
                request.Notes);
        }
        else
        {
            var newScope = new UserAccessScope(
                request.UserId,
                request.RoleDefinitionId,
                request.PanelScope,
                request.ScopeType,
                normalizedScopeEntityId,
                request.Notes);

            _context.UserAccessScopes.Add(newScope);
        }

        user.IncrementPermissionVersion();
        _auditService.Add(
            user.Id,
            "access-scope-updated",
            $"Access scope updated with role {role.Code}.",
            after: new
            {
                user.Id,
                RoleDefinitionId = role.Id,
                RoleCode = role.Code,
                PanelScope = request.PanelScope.ToString(),
                ScopeType = request.ScopeType.ToString(),
                ScopeEntityId = normalizedScopeEntityId
            });

        await _context.SaveChangesAsync(cancellationToken);
    }
}
