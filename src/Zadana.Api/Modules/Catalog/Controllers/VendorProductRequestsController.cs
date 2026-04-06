using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Catalog.Requests;
using Zadana.Application.Modules.Catalog.Commands.ProductRequests.SubmitRequest;
using Zadana.Application.Modules.Catalog.Queries.ProductRequests.GetVendorRequests;

namespace Zadana.Api.Modules.Catalog.Controllers;

[Route("api/vendor/product-requests")]
[Tags("Catalog Vendors")]
[Authorize(Roles = "Vendor")]
public class VendorProductRequestsController : ApiControllerBase
{
    [HttpPost]
    public async Task<IActionResult> SubmitRequest([FromBody] SubmitProductRequestRequest request)
    {
        var command = new SubmitProductRequestCommand(
            request.SuggestedNameAr,
            request.SuggestedNameEn,
            request.SuggestedCategoryId,
            request.SuggestedDescriptionAr,
            request.SuggestedDescriptionEn,
            request.ImageUrl);

        var requestId = await Sender.Send(command);
        return Ok(new { ProductRequestId = requestId });
    }

    [HttpGet]
    public async Task<IActionResult> GetMyRequests([FromQuery] GetVendorProductRequestsRequest request)
    {
        var query = new GetVendorProductRequestsQuery(request.PageNumber, request.PageSize, request.Status);
        var result = await Sender.Send(query);
        return Ok(result);
    }
}
