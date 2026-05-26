using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Catalog.Requests;
using Zadana.Application.Modules.Catalog.Commands.Units.CreateUnit;
using Zadana.Application.Modules.Catalog.Commands.Units.DeleteUnit;
using Zadana.Application.Modules.Catalog.Commands.Units.UpdateUnit;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Application.Modules.Catalog.Queries.Units.GetUnitById;
using Zadana.Application.Modules.Catalog.Queries.Units.GetUnits;

namespace Zadana.Api.Modules.Catalog.Controllers;

[Route("api/admin/catalog/units")]
[Authorize(Roles = "Admin,SuperAdmin")]
[Tags("Catalog (Admins)")]
public class AdminUnitsController : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<UnitOfMeasureDto>>> GetUnits([FromQuery] bool includeInactive = false)
    {
        var result = await Sender.Send(new GetUnitsQuery(includeInactive));
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UnitOfMeasureDto>> GetUnit(Guid id)
    {
        var result = await Sender.Send(new GetUnitByIdQuery(id));
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<UnitOfMeasureDto>> CreateUnit([FromBody] CreateUnitRequest request)
    {
        var result = await Sender.Send(new CreateUnitCommand(request.NameAr, request.NameEn, request.Symbol, request.Kind));
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
            request.Kind,
            request.IsActive);

        await Sender.Send(command);
        return Ok();
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteUnit(Guid id)
    {
        await Sender.Send(new DeleteUnitCommand(id));
        return NoContent();
    }
}

