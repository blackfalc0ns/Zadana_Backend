using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Application.Modules.Geography.DTOs;
using Zadana.Application.Modules.Geography.Queries.GetAdminGeographyCoverage;

namespace Zadana.Api.Modules.Geography.Controllers;

[ApiController]
[Route("api/admin/geography")]
[Authorize(Policy = "AdminOnly")]
public class AdminGeographyController(IMediator mediator) : ControllerBase
{
    [HttpGet("coverage")]
    [ProducesResponseType(typeof(AdminGeographyCoverageDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminGeographyCoverageDto>> GetCoverage(
        [FromQuery] string region = "all",
        [FromQuery] bool gapsOnly = false,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetAdminGeographyCoverageQuery(region, gapsOnly), cancellationToken);
        return Ok(result);
    }
}
