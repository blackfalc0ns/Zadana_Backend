using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Api.Authorization;
using Zadana.Domain.Modules.Identity.Constants;
using Microsoft.AspNetCore.Authorization;

namespace Zadana.Api.Modules.Identity.Controllers;

[Route("api/vendors")]
[Authorize]
public class VendorAccessController : ApiControllerBase
{
    [HttpGet("branches")]
    [RequireAccess(PermissionKeys.Vendor.BranchTeamView)]
    public IActionResult GetBranches()
    {
        return Ok(Array.Empty<object>());
    }

    [HttpGet("staff")]
    [RequireAccess(PermissionKeys.Vendor.BranchTeamView)]
    public IActionResult GetStaff()
    {
        return Ok(Array.Empty<object>());
    }

    [HttpGet("staff/{id}")]
    [RequireAccess(PermissionKeys.Vendor.BranchTeamView)]
    public IActionResult GetStaffMember(Guid id)
    {
        return NotFound();
    }

    [HttpPost("staff/invitations")]
    [RequireAccess(PermissionKeys.Vendor.BranchTeamCreate)]
    public IActionResult CreateInvitation([FromBody] object request)
    {
        return Ok();
    }

    [HttpPut("staff/{id}/role")]
    [RequireAccess(PermissionKeys.Vendor.BranchTeamEdit)]
    public IActionResult UpdateStaffRole(Guid id, [FromBody] object request)
    {
        return Ok();
    }

    [HttpPut("staff/{id}/scope")]
    [RequireAccess(PermissionKeys.Vendor.BranchTeamEdit)]
    public IActionResult UpdateStaffScope(Guid id, [FromBody] object request)
    {
        return Ok();
    }

    [HttpPut("staff/{id}/overrides")]
    [RequireAccess(PermissionKeys.Vendor.BranchTeamEdit)]
    public IActionResult UpdateStaffOverrides(Guid id, [FromBody] object request)
    {
        return Ok();
    }

    [HttpGet("staff/{id}/effective-access")]
    [RequireAccess(PermissionKeys.Vendor.BranchTeamView)]
    public IActionResult GetStaffEffectiveAccess(Guid id)
    {
        return Ok();
    }

    [HttpPost("staff/invitations/{id}/resend")]
    [RequireAccess(PermissionKeys.Vendor.BranchTeamEdit)]
    public IActionResult ResendInvitation(Guid id)
    {
        return Ok();
    }

    [HttpPost("staff/invitations/{id}/revoke")]
    [RequireAccess(PermissionKeys.Vendor.BranchTeamEdit)]
    public IActionResult RevokeInvitation(Guid id)
    {
        return Ok();
    }
}
