using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Api.Authorization;
using Zadana.Domain.Modules.Identity.Constants;
using Microsoft.AspNetCore.Authorization;
using MediatR;
using Zadana.Application.Modules.Identity.Queries.GetRoles;
using Zadana.Application.Modules.Identity.Queries.GetAdminUsers;
using Zadana.Application.Modules.Identity.Queries.GetUserEffectiveAccess;
using Zadana.Application.Modules.Identity.Commands.CreateRole;
using Zadana.Application.Modules.Identity.Commands.UpdateRole;
using Zadana.Application.Modules.Identity.Commands.DeleteRole;
using Zadana.Application.Modules.Identity.Commands.UpdateUserScope;
using Zadana.Application.Modules.Identity.Commands.UpdateUserOverrides;
using Zadana.Application.Modules.Identity.Commands.AdminAccessUsers;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.Services;
using Zadana.Domain.Modules.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Identity.Controllers;

[Route("api/admin/access")]
[Authorize(Policy = "AdminOnly")]
public class AdminAccessController(
    IMediator mediator,
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService,
    IAccessAuditService auditService) : ApiControllerBase
{
    [HttpGet("permissions")]
    [RequireAccess(PermissionKeys.Admin.UsersAccessView)]
    public async Task<IActionResult> GetPermissions(CancellationToken cancellationToken)
    {
        var permissions = await dbContext.PermissionDefinitions
            .AsNoTracking()
            .OrderBy(x => x.PanelScope)
            .ThenBy(x => x.Domain)
            .ThenBy(x => x.Action)
            .Select(x => new PermissionDefinitionDto(
                x.Id,
                x.Key,
                x.Name,
                x.Domain,
                x.Action,
                x.PanelScope,
                x.Description,
                x.IsSensitive))
            .ToListAsync(cancellationToken);

        return Ok(permissions);
    }

    [HttpGet("roles")]
    [RequireAccess(PermissionKeys.Admin.UsersAccessView)]
    public async Task<IActionResult> GetRoles()
    {
        var result = await mediator.Send(new GetRolesQuery());
        return Ok(result);
    }

    [HttpGet("roles/{id}")]
    [RequireAccess(PermissionKeys.Admin.UsersAccessView)]
    public async Task<IActionResult> GetRole(Guid id, CancellationToken cancellationToken)
    {
        var role = await dbContext.RoleDefinitions
            .AsNoTracking()
            .Include(item => item.RolePermissions)
            .ThenInclude(item => item.PermissionDefinition)
            .Include(item => item.UserAccessScopes)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (role is null)
        {
            return NotFound();
        }

        return Ok(new RoleDefinitionDto(
            role.Id,
            role.Code,
            role.Name,
            role.Description,
            role.IsSystem,
            role.IsActive,
            role.IdentityRole,
            role.PanelScope,
            role.RolePermissions
                .Select(item => item.PermissionDefinition.Key)
                .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            role.UserAccessScopes.Count));
    }

    [HttpPost("roles")]
    [RequireAccess(PermissionKeys.Admin.UsersAccessCreate)]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleCommand command)
    {
        var result = await mediator.Send(command);
        return Ok(result);
    }

    [HttpPut("roles/{id}")]
    [RequireAccess(PermissionKeys.Admin.UsersAccessEdit)]
    public async Task<IActionResult> UpdateRole(Guid id, [FromBody] UpdateRoleCommand command)
    {
        if (id != command.Id) return BadRequest();
        var result = await mediator.Send(command);
        return Ok(result);
    }

    [HttpDelete("roles/{id}")]
    [RequireAccess(PermissionKeys.Admin.UsersAccessEdit)]
    public async Task<IActionResult> DeleteRole(Guid id)
    {
        await mediator.Send(new DeleteRoleCommand(id));
        return NoContent();
    }

    [HttpGet("users")]
    [RequireAccess(PermissionKeys.Admin.UsersAccessView)]
    public async Task<IActionResult> GetUsers(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? roleDefinitionId = null,
        [FromQuery] PanelScope? panelScope = null)
    {
        var result = await mediator.Send(new GetAdminUsersQuery(
            pageNumber,
            pageSize,
            search,
            status,
            roleDefinitionId,
            panelScope));
        return Ok(result);
    }

    [HttpGet("users/{id}")]
    [RequireAccess(PermissionKeys.Admin.UsersAccessView)]
    public async Task<IActionResult> GetUser(Guid id)
    {
        var result = await mediator.Send(new GetAdminUserByIdQuery(id));
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("users")]
    [RequireAccess(PermissionKeys.Admin.UsersAccessCreate)]
    public async Task<IActionResult> CreateUser([FromBody] CreateAdminAccessUserRequest request)
    {
        var result = await mediator.Send(new CreateAdminAccessUserCommand(
            request.FullName,
            request.Email,
            request.Phone,
            request.Password,
            request.RoleDefinitionId,
            request.PanelScope,
            request.ScopeType,
            request.ScopeEntityId,
            request.Department,
            request.Team,
            request.Notes));

        return Ok(result);
    }

    [HttpPut("users/{id}")]
    [RequireAccess(PermissionKeys.Admin.UsersAccessEdit)]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateAdminAccessUserRequest request)
    {
        var result = await mediator.Send(new UpdateAdminAccessUserCommand(
            id,
            request.FullName,
            request.Email,
            request.Phone,
            request.RoleDefinitionId,
            request.PanelScope,
            request.ScopeType,
            request.ScopeEntityId,
            request.Department,
            request.Team,
            request.Status,
            request.Notes,
            request.GrantedPermissions,
            request.RevokedPermissions,
            request.Communication));

        return Ok(result);
    }

    [HttpPost("users/{id}/temporary-password")]
    [RequireAccess(PermissionKeys.Admin.UsersAccessEdit)]
    public async Task<IActionResult> ResetTemporaryPassword(Guid id, [FromBody] ResetTemporaryPasswordRequest request)
    {
        var result = await mediator.Send(new ResetAdminAccessUserTemporaryPasswordCommand(
            id,
            request.TemporaryPassword));

        return Ok(result);
    }

    [HttpGet("users/{id}/audit")]
    [RequireAccess(PermissionKeys.Admin.UsersAccessView)]
    public async Task<IActionResult> GetUserAudit(Guid id, CancellationToken cancellationToken)
    {
        var result = await (
                from log in dbContext.AccessAuditLogs.AsNoTracking()
                join actor in dbContext.Users.AsNoTracking()
                    on log.ActorUserId equals (Guid?)actor.Id into actorJoin
                from actor in actorJoin.DefaultIfEmpty()
                where log.TargetUserId == id
                orderby log.CreatedAtUtc descending
                select new AccessAuditLogDto(
                    log.Id,
                    log.ActorUserId,
                    actor != null ? actor.FullName : null,
                    actor != null ? actor.Email : null,
                    log.TargetUserId,
                    log.Action,
                    log.Summary,
                    log.BeforeJson,
                    log.AfterJson,
                    log.CreatedAtUtc.ToString("o"),
                    log.IpAddress,
                    log.UserAgent))
            .Take(100)
            .ToListAsync(cancellationToken);

        return Ok(result);
    }

    [HttpPut("users/{id}/scope")]
    [RequireAccess(PermissionKeys.Admin.UsersAccessEdit)]
    public async Task<IActionResult> UpdateUserScope(Guid id, [FromBody] UpdateUserScopeRequest request)
    {
        await mediator.Send(new UpdateUserScopeCommand(
            UserId: id,
            RoleDefinitionId: request.RoleDefinitionId,
            PanelScope: request.PanelScope,
            ScopeType: request.ScopeType,
            ScopeEntityId: request.ScopeEntityId,
            Notes: request.Notes
        ));
        return Ok();
    }

    [HttpPut("users/{id}/overrides")]
    [RequireAccess(PermissionKeys.Admin.UsersAccessEdit)]
    public async Task<IActionResult> UpdateUserOverrides(Guid id, [FromBody] UpdateUserOverridesRequest request)
    {
        await mediator.Send(new UpdateUserOverridesCommand(
            UserId: id,
            GrantedPermissions: request.GrantedPermissions,
            RevokedPermissions: request.RevokedPermissions
        ));
        return Ok();
    }

    [HttpGet("users/{id}/effective-access")]
    [RequireAccess(PermissionKeys.Admin.UsersAccessView)]
    public async Task<IActionResult> GetUserEffectiveAccess(Guid id)
    {
        var result = await mediator.Send(new GetUserEffectiveAccessQuery(id));
        return Ok(result);
    }

    [HttpGet("approvals")]
    [RequireAccess(PermissionKeys.Admin.UsersAccessApprove)]
    public async Task<IActionResult> GetApprovals(
        [FromQuery] AccessApprovalStatus? status = AccessApprovalStatus.Pending,
        [FromQuery] Guid? requestedByUserId = null,
        [FromQuery] Guid? targetUserId = null,
        [FromQuery] int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 250);

        var query = dbContext.AccessApprovalRequests.AsNoTracking();
        if (status.HasValue)
        {
            query = query.Where(request => request.Status == status.Value);
        }

        if (requestedByUserId.HasValue)
        {
            query = query.Where(request => request.RequestedByUserId == requestedByUserId.Value);
        }

        if (targetUserId.HasValue)
        {
            query = query.Where(request => request.TargetUserId == targetUserId.Value);
        }

        var result = await ProjectApprovalRequests(query.OrderByDescending(request => request.CreatedAtUtc))
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Ok(result);
    }

    [HttpPost("approvals/{id}/approve")]
    [RequireAccess(PermissionKeys.Admin.UsersAccessApprove)]
    public async Task<IActionResult> ApproveRequest(
        Guid id,
        [FromBody] AccessApprovalDecisionRequest? request,
        CancellationToken cancellationToken)
    {
        var approval = await dbContext.AccessApprovalRequests
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (approval is null)
        {
            return NotFound();
        }

        EnsureCurrentUserCanDecideApproval(approval);
        var before = SnapshotApproval(approval);
        approval.Approve(currentUserService.UserId!.Value, request?.Note);
        auditService.Add(
            approval.TargetUserId ?? approval.RequestedByUserId,
            "access-approval-approved",
            $"Access approval request {approval.Action} was approved.",
            before,
            SnapshotApproval(approval));

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await ProjectApprovalRequestAsync(id, cancellationToken));
    }

    [HttpPost("approvals/{id}/reject")]
    [RequireAccess(PermissionKeys.Admin.UsersAccessApprove)]
    public async Task<IActionResult> RejectRequest(
        Guid id,
        [FromBody] AccessApprovalDecisionRequest? request,
        CancellationToken cancellationToken)
    {
        var approval = await dbContext.AccessApprovalRequests
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (approval is null)
        {
            return NotFound();
        }

        EnsureCurrentUserCanDecideApproval(approval);
        var before = SnapshotApproval(approval);
        approval.Reject(currentUserService.UserId!.Value, request?.Note);
        auditService.Add(
            approval.TargetUserId ?? approval.RequestedByUserId,
            "access-approval-rejected",
            $"Access approval request {approval.Action} was rejected.",
            before,
            SnapshotApproval(approval));

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await ProjectApprovalRequestAsync(id, cancellationToken));
    }

    [HttpGet("audit")]
    [RequireAccess(PermissionKeys.Admin.UsersAccessView)]
    public async Task<IActionResult> GetAuditEvents(
        [FromQuery] Guid? targetUserId = null,
        [FromQuery] Guid? actorUserId = null,
        [FromQuery] string? action = null,
        [FromQuery] int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 250);

        var query = dbContext.AccessAuditLogs.AsNoTracking();
        if (targetUserId.HasValue)
        {
            query = query.Where(log => log.TargetUserId == targetUserId.Value);
        }

        if (actorUserId.HasValue)
        {
            query = query.Where(log => log.ActorUserId == actorUserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            var normalizedAction = action.Trim();
            query = query.Where(log => log.Action == normalizedAction);
        }

        var result = await (
                from log in query
                join actor in dbContext.Users.AsNoTracking()
                    on log.ActorUserId equals (Guid?)actor.Id into actorJoin
                from actor in actorJoin.DefaultIfEmpty()
                orderby log.CreatedAtUtc descending
                select new AccessAuditLogDto(
                    log.Id,
                    log.ActorUserId,
                    actor != null ? actor.FullName : null,
                    actor != null ? actor.Email : null,
                    log.TargetUserId,
                    log.Action,
                    log.Summary,
                    log.BeforeJson,
                    log.AfterJson,
                    log.CreatedAtUtc.ToString("o"),
                    log.IpAddress,
                    log.UserAgent))
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Ok(result);
    }

    private void EnsureCurrentUserCanDecideApproval(AccessApprovalRequest approval)
    {
        if (!currentUserService.UserId.HasValue)
        {
            throw new BusinessRuleException("APPROVER_REQUIRED", "An authenticated approver is required.");
        }

        if (approval.RequestedByUserId == currentUserService.UserId.Value)
        {
            throw new BusinessRuleException("SELF_APPROVAL_BLOCKED", "You cannot approve or reject your own access request.");
        }
    }

    private async Task<AccessApprovalRequestDto?> ProjectApprovalRequestAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        await ProjectApprovalRequests(dbContext.AccessApprovalRequests.AsNoTracking().Where(item => item.Id == id))
            .FirstOrDefaultAsync(cancellationToken);

    private IQueryable<AccessApprovalRequestDto> ProjectApprovalRequests(IQueryable<AccessApprovalRequest> query) =>
        from approval in query
        join requestedBy in dbContext.Users.AsNoTracking()
            on approval.RequestedByUserId equals requestedBy.Id
        join target in dbContext.Users.AsNoTracking()
            on approval.TargetUserId equals (Guid?)target.Id into targetJoin
        from target in targetJoin.DefaultIfEmpty()
        join decidedBy in dbContext.Users.AsNoTracking()
            on approval.DecidedByUserId equals (Guid?)decidedBy.Id into decidedByJoin
        from decidedBy in decidedByJoin.DefaultIfEmpty()
        select new AccessApprovalRequestDto(
            approval.Id,
            approval.RequestedByUserId,
            requestedBy.FullName,
            requestedBy.Email,
            approval.TargetUserId,
            target != null ? target.FullName : null,
            target != null ? target.Email : null,
            approval.Action,
            approval.Summary,
            approval.PayloadHash,
            approval.PayloadJson,
            approval.Status.ToString(),
            approval.CreatedAtUtc.ToString("o"),
            approval.DecidedByUserId,
            decidedBy != null ? decidedBy.FullName : null,
            decidedBy != null ? decidedBy.Email : null,
            approval.DecidedAtUtc == null ? null : approval.DecidedAtUtc.GetValueOrDefault().ToString("o"),
            approval.DecisionNote,
            approval.ConsumedAtUtc == null ? null : approval.ConsumedAtUtc.GetValueOrDefault().ToString("o"));

    private static object SnapshotApproval(AccessApprovalRequest approval) => new
    {
        approval.Id,
        approval.RequestedByUserId,
        approval.TargetUserId,
        approval.Action,
        approval.Summary,
        approval.PayloadHash,
        Status = approval.Status.ToString(),
        approval.CreatedAtUtc,
        approval.DecidedByUserId,
        approval.DecidedAtUtc,
        approval.DecisionNote,
        approval.ConsumedAtUtc
    };
}

// Request DTOs
public record UpdateUserScopeRequest(
    Guid RoleDefinitionId,
    PanelScope PanelScope,
    AccessScopeType ScopeType,
    Guid? ScopeEntityId,
    string? Notes
);

public record UpdateUserOverridesRequest(
    List<string> GrantedPermissions,
    List<string> RevokedPermissions
);

public record CreateAdminAccessUserRequest(
    string FullName,
    string Email,
    string Phone,
    string Password,
    Guid RoleDefinitionId,
    PanelScope PanelScope,
    AccessScopeType ScopeType,
    Guid? ScopeEntityId,
    string? Department,
    string? Team,
    string? Notes
);

public record UpdateAdminAccessUserRequest(
    string FullName,
    string Email,
    string Phone,
    Guid RoleDefinitionId,
    PanelScope PanelScope,
    AccessScopeType ScopeType,
    Guid? ScopeEntityId,
    string? Department,
    string? Team,
    string? Status,
    string? Notes,
    List<string> GrantedPermissions,
    List<string> RevokedPermissions,
    DirectoryCommunicationProfileDto? Communication
);

public record ResetTemporaryPasswordRequest(string TemporaryPassword);

public record AccessApprovalDecisionRequest(string? Note);
