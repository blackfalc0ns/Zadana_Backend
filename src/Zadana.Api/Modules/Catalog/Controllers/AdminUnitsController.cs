using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Application.Modules.Catalog.Commands.Units.CreateUnit;
using Zadana.Application.Modules.Catalog.Commands.Units.UpdateUnit;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Application.Modules.Catalog.Queries.Units.GetUnits;

namespace Zadana.Api.Modules.Catalog.Controllers;

[ApiController]
[Route("api/admin/catalog/units")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class AdminUnitsController : ControllerBase
{
    private readonly ISender _sender;

    public AdminUnitsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<ActionResult<List<UnitOfMeasureDto>>> GetUnits([FromQuery] bool includeInactive = false)
    {
        var result = await _sender.Send(new GetUnitsQuery(includeInactive));
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<UnitOfMeasureDto>> CreateUnit(CreateUnitCommand command)
    {
        var result = await _sender.Send(command);
        return Ok(result);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> UpdateUnit(Guid id, [FromBody] UpdateUnitRequest request)
    {
        var command = new UpdateUnitCommand(
            id,
            request.NameAr,
            request.NameEn,
            request.Symbol,
            request.IsActive);

        await _sender.Send(command);
        return Ok();
    }
}

public record UpdateUnitRequest(
    string NameAr,
    string NameEn,
    string? Symbol,
    bool IsActive);
