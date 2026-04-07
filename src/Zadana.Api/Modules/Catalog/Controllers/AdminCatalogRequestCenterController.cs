using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Catalog.Requests;
using Zadana.Application.Modules.Catalog.Interfaces;

namespace Zadana.Api.Modules.Catalog.Controllers;

[Route("api/admin/catalog/request-center")]
[Tags("Catalog Admins")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class AdminCatalogRequestCenterController : ApiControllerBase
{
    private readonly ICatalogRequestReadService _catalogRequestReadService;

    public AdminCatalogRequestCenterController(ICatalogRequestReadService catalogRequestReadService)
    {
        _catalogRequestReadService = catalogRequestReadService;
    }

    [HttpGet]
    public async Task<IActionResult> GetRequests([FromQuery] GetCatalogRequestCenterRequest request)
    {
        var result = await _catalogRequestReadService.GetAdminRequestsAsync(
            request.Type,
            request.Status,
            request.VendorId,
            request.PageNumber,
            request.PageSize,
            HttpContext.RequestAborted);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetRequestDetail(Guid id, [FromQuery] string type)
    {
        var result = await _catalogRequestReadService.GetAdminRequestDetailAsync(type, id, HttpContext.RequestAborted);
        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }
}
