using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Application.Modules.Dashboard.DTOs;
using Zadana.Application.Modules.Dashboard.Queries.GetAdminDashboardOverview;

namespace Zadana.Api.Modules.Dashboard.Controllers;

[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Policy = "AdminOnly")]
public class AdminDashboardController(IMediator mediator) : ControllerBase
{
    [HttpGet("overview")]
    [ProducesResponseType(typeof(AdminDashboardOverviewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminDashboardOverviewDto>> GetOverview(
        [FromQuery] string period = "today",
        [FromQuery] string region = "all",
        [FromQuery] Guid? vendorId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetAdminDashboardOverviewQuery(period, region, vendorId), cancellationToken);
        return Ok(result);
    }
}
