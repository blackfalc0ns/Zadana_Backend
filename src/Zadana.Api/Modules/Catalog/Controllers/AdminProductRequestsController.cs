using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Catalog.Requests;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Catalog.Commands.ProductRequests.ReviewRequest;
using Zadana.Application.Modules.Catalog.Queries.ProductRequests.GetPendingRequests;

namespace Zadana.Api.Modules.Catalog.Controllers;

[Route("api/admin/product-requests")]
[Route("api/admin/catalog/product-requests")]
[Tags("Catalog Admins")]
[Authorize(Policy = "AdminOnly")]
public class AdminProductRequestsController : ApiControllerBase
{
    private readonly IStringLocalizer<SharedResource> _localizer;

    public AdminProductRequestsController(IStringLocalizer<SharedResource> localizer)
    {
        _localizer = localizer;
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingRequests([FromQuery] GetPendingProductRequestsRequest request)
    {
        var query = new GetPendingProductRequestsQuery(request.PageNumber, request.PageSize);
        var result = await Sender.Send(query);
        return Ok(result);
    }

    [HttpPost("{id:guid}/review")]
    public async Task<IActionResult> ReviewRequest(Guid id, [FromBody] ReviewProductRequestRequest request)
    {
        var command = new ReviewProductRequestCommand(id, request.IsApproved, request.RejectionReason);
        var masterProductId = await Sender.Send(command);

        return Ok(new
        {
            Message = request.IsApproved
                ? _localizer["PRODUCT_REQUEST_APPROVED"].Value
                : _localizer["PRODUCT_REQUEST_REJECTED"].Value,
            MasterProductId = masterProductId
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetPendingRequestsFlat([FromQuery] GetPendingProductRequestsRequest request)
    {
        var query = new GetPendingProductRequestsQuery(request.PageNumber, request.PageSize);
        var result = await Sender.Send(query);
        return Ok(result);
    }
}
