using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Catalog.Requests;
using Zadana.Application.Modules.Catalog.Commands.Units.CreateUnit;
using Zadana.Application.Modules.Catalog.Commands.Units.UpdateUnit;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Application.Modules.Catalog.Queries.Units.GetUnits;

namespace Zadana.Api.Modules.Catalog.Controllers;

[Route("api/admin/catalog/units")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class AdminUnitsController : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<UnitOfMeasureDto>>> GetUnits([FromQuery] bool includeInactive = false)
    {
        var result = await Sender.Send(new GetUnitsQuery(includeInactive));
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<UnitOfMeasureDto>> CreateUnit([FromBody] CreateUnitRequest request)
    {
        var result = await Sender.Send(new CreateUnitCommand(request.NameAr, request.NameEn, request.Symbol));
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

        await Sender.Send(command);
        return Ok();
    }
}
