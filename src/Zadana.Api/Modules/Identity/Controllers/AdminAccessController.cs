using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Api.Authorization;
using Zadana.Domain.Modules.Identity.Constants;
using Microsoft.AspNetCore.Authorization;
using MediatR;
using Zadana.Application.Modules.Identity.Queries.GetRoles;
using Zadana.Application.Modules.Identity.Queries.GetAdminUsers;
using Zadana.Application.Modules.Identity.Commands.CreateRole;
using Zadana.Application.Modules.Identity.Commands.UpdateRole;
using Zadana.Application.Modules.Identity.Commands.DeleteRole;

namespace Zadana.Api.Modules.Identity.Controllers;

[Route("api/admin/access")]
[Authorize]
public class AdminAccessController(IMediator mediator) : ApiControllerBase
{
    [HttpGet("permissions")]
    [RequireAccess(PermissionKeys.Admin.UsersAccessView)]
    public IActionResult GetPermissions()
    {
        return Ok(PermissionKeys.Admin.All);
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
    public async Task<IActionResult> GetUsers()
    {
        var result = await mediator.Send(new GetAdminUsersQuery());
        return Ok(result);
    }

    [HttpGet("users/{id}")]
    [RequireAccess(PermissionKeys.Admin.UsersAccessView)]
    public async Task<IActionResult> GetUser(Guid id)
    {
        var result = await mediator.Send(new GetAdminUserByIdQuery(id));
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("users/{id}/scope")]
    [RequireAccess(PermissionKeys.Admin.UsersAccessEdit)]
    public IActionResult UpdateUserScope(Guid id, [FromBody] object request)
    {
        return Ok();
    }

    [HttpPut("users/{id}/overrides")]
    [RequireAccess(PermissionKeys.Admin.UsersAccessEdit)]
    public IActionResult UpdateUserOverrides(Guid id, [FromBody] object request)
    {
        return Ok();
    }

    [HttpGet("users/{id}/effective-access")]
    [RequireAccess(PermissionKeys.Admin.UsersAccessView)]
    public IActionResult GetUserEffectiveAccess(Guid id)
    {
        return Ok();
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
