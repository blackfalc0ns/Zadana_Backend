using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Catalog.Requests;
using Zadana.Application.Modules.Catalog.Commands.CategoryRequests.ReviewRequest;

namespace Zadana.Api.Modules.Catalog.Controllers;

[Route("api/admin/catalog/category-requests")]
[Tags("Catalog Admins")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class AdminCategoryRequestsController : ApiControllerBase
{
    [HttpPost("{id:guid}/review")]
    public async Task<IActionResult> ReviewRequest(Guid id, [FromBody] ReviewProductRequestRequest request)
    {
        var createdCategoryId = await Sender.Send(new ReviewCategoryRequestCommand(id, request.IsApproved, request.RejectionReason));
        return Ok(new { CreatedCategoryId = createdCategoryId });
    }
}
