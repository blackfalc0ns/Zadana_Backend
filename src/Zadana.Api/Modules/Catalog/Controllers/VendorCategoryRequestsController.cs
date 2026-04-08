using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Catalog.Requests;
using Zadana.Application.Modules.Catalog.Commands.CategoryRequests.SubmitRequest;

namespace Zadana.Api.Modules.Catalog.Controllers;

[Route("api/vendor/catalog/category-requests")]
[Tags("Catalog (Vendors)")]
[Authorize(Roles = "Vendor")]
public class VendorCategoryRequestsController : ApiControllerBase
{
    [HttpPost]
    public async Task<IActionResult> SubmitRequest([FromBody] SubmitCategoryRequestPayload request)
    {
        var requestId = await Sender.Send(new SubmitCategoryRequestCommand(
            request.NameAr,
            request.NameEn,
            request.ParentCategoryId,
            request.DisplayOrder,
            request.ImageUrl));

        return Ok(new { CategoryRequestId = requestId });
    }
}

