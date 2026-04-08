using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Catalog.Requests;
using Zadana.Application.Modules.Catalog.Commands.BrandRequests.ReviewRequest;

namespace Zadana.Api.Modules.Catalog.Controllers;

[Route("api/admin/catalog/brand-requests")]
[Tags("Catalog (Admins)")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class AdminBrandRequestsController : ApiControllerBase
{
    [HttpPost("{id:guid}/review")]
    public async Task<IActionResult> ReviewRequest(Guid id, [FromBody] ReviewProductRequestRequest request)
    {
        var createdBrandId = await Sender.Send(new ReviewBrandRequestCommand(id, request.IsApproved, request.RejectionReason));
        return Ok(new { CreatedBrandId = createdBrandId });
    }
}

