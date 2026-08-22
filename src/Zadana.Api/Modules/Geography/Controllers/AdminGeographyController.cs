using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Application.Modules.Geography.Commands.UpdateOperationalCity;
using Zadana.Application.Modules.Geography.Commands.UpdateOperationalRegion;
using Zadana.Application.Modules.Geography.DTOs;
using Zadana.Application.Modules.Geography.Queries.GetAdminGeographyCoverage;
using Zadana.Application.Modules.Geography.Queries.GetAdminOperationalRegions;

namespace Zadana.Api.Modules.Geography.Controllers;

[ApiController]
[Route("api/admin/geography")]
[Authorize(Policy = "AdminOnly")]
public class AdminGeographyController : ApiControllerBase
{
    [HttpGet("coverage")]
    [ProducesResponseType(typeof(AdminGeographyCoverageDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminGeographyCoverageDto>> GetCoverage(
        [FromQuery] string region = "all",
        [FromQuery] bool gapsOnly = false,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new GetAdminGeographyCoverageQuery(region, gapsOnly), cancellationToken);
        return Ok(result);
    }

    [HttpGet("operational-regions")]
    [ProducesResponseType(typeof(IReadOnlyList<OperationalRegionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OperationalRegionDto>>> GetOperationalRegions(
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new GetAdminOperationalRegionsQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpPut("operational-regions/{regionCode}")]
    [ProducesResponseType(typeof(OperationalRegionDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationalRegionDto>> UpdateOperationalRegion(
        string regionCode,
        [FromBody] UpdateOperationalRegionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(
            new UpdateOperationalRegionCommand(regionCode, request.IsOperational),
            cancellationToken);
        return Ok(result);
    }

    [HttpPut("operational-cities/{cityCode}")]
    [ProducesResponseType(typeof(OperationalCityDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationalCityDto>> UpdateOperationalCity(
        string cityCode,
        [FromBody] UpdateOperationalCityRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(
            new UpdateOperationalCityCommand(cityCode, request.IsOperational),
            cancellationToken);
        return Ok(result);
    }
}

public sealed record UpdateOperationalRegionRequest(bool IsOperational);

public sealed record UpdateOperationalCityRequest(bool IsOperational);
