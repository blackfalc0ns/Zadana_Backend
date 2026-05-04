using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Application.Modules.Finances.DTOs;
using Zadana.Application.Modules.Finances.Queries.GetAdminFinanceDashboard;
using Zadana.Application.Modules.Finances.Queries.GetZoneFinanceSettings;
using Zadana.Application.Modules.Finances.Commands.UpdateZoneFinanceSettings;

namespace Zadana.Api.Modules.Finances.Controllers;

[ApiController]
[Route("api/admin/finances")]
[Authorize(Policy = "AdminOnly")]
public class AdminFinancesController(IMediator mediator) : ControllerBase
{
    [HttpGet("dashboard/snapshot")]
    [ProducesResponseType(typeof(AdminFinanceDashboardDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminFinanceDashboardDto>> GetDashboardSnapshot(
        [FromQuery] string period = "month",
        CancellationToken cancellationToken = default)
    {
        var query = new GetAdminFinanceDashboardQuery(period);
        var result = await mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("pricing-settings")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ZoneFinanceSettingsDto>>> GetPricingSettings(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetZoneFinanceSettingsQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpPut("pricing-settings/{zoneId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ZoneFinanceSettingsDto>> UpdatePricingSettings(
        [FromRoute] Guid zoneId,
        [FromBody] UpdateZoneFinanceSettingsCommand command,
        CancellationToken cancellationToken)
    {
        if (zoneId != command.ZoneId) return BadRequest("ZoneId mismatch");

        var result = await mediator.Send(command, cancellationToken);
        return Ok(result);
    }
}
