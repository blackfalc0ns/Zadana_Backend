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
using Microsoft.EntityFrameworkCore;

namespace Zadana.Api.Modules.Identity.Controllers;

[Route("api/admin/access")]
[Authorize]
public class AdminAccessController(IMediator mediator, IApplicationDbContext dbContext) : ApiControllerBase
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
    public IActionResult GetRole(Guid id)
    {
        // For individual role, we can fetch all and filter or write a specific query
        // Currently returning NotFound for simplicity as UI only needs GetRoles
        return NotFound();
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
    public IActionResult GetApprovals()
    {
        return Ok(Array.Empty<object>());
    }

    [HttpPost("approvals/{id}/approve")]
    [RequireAccess(PermissionKeys.Admin.UsersAccessApprove)]
    public IActionResult ApproveRequest(Guid id)
    {
        return Ok();
    }

    [HttpPost("approvals/{id}/reject")]
    [RequireAccess(PermissionKeys.Admin.UsersAccessApprove)]
    public IActionResult RejectRequest(Guid id)
    {
        return Ok();
    }

    [HttpGet("audit")]
    [RequireAccess(PermissionKeys.Admin.UsersAccessView)]
    public IActionResult GetAuditEvents()
    {
        return Ok(Array.Empty<object>());
    }
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
