using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Marketing.Requests;
using Zadana.Application.Modules.Marketing.Commands.FeaturedPlacements;
using Zadana.Application.Modules.Marketing.DTOs;
using Zadana.Application.Modules.Marketing.Queries.FeaturedPlacements;

namespace Zadana.Api.Modules.Marketing.Controllers;

[Route("api/admin/marketing/featured-products")]
[Authorize(Policy = "AdminOnly")]
[Tags("Marketing (Admins)")]
public class AdminMarketingFeaturedProductsController : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<FeaturedProductPlacementDto>>> GetPlacements()
    {
        var result = await Sender.Send(new GetFeaturedProductPlacementsQuery());
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FeaturedProductPlacementDto>> GetPlacement(Guid id)
    {
        var result = await Sender.Send(new GetFeaturedProductPlacementByIdQuery(id));
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<FeaturedProductPlacementDto>> CreatePlacement([FromBody] CreateFeaturedPlacementRequest request)
    {
        var result = await Sender.Send(new CreateFeaturedProductPlacementCommand(
            request.PlacementType, request.VendorProductId, request.MasterProductId,
            request.DisplayOrder, request.StartsAtUtc, request.EndsAtUtc, request.Note));
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<FeaturedProductPlacementDto>> UpdatePlacement(Guid id, [FromBody] UpdateFeaturedPlacementRequest request)
    {
        var result = await Sender.Send(new UpdateFeaturedProductPlacementCommand(
            id, request.PlacementType, request.VendorProductId, request.MasterProductId,
            request.DisplayOrder, request.StartsAtUtc, request.EndsAtUtc, request.IsActive, request.Note));
        return Ok(result);
    }

    [HttpPatch("{id:guid}/activate")]
    public async Task<ActionResult> Activate(Guid id)
    {
        await Sender.Send(new ActivateFeaturedProductPlacementCommand(id));
        return NoContent();
    }

    [HttpPatch("{id:guid}/deactivate")]
    public async Task<ActionResult> Deactivate(Guid id)
    {
        await Sender.Send(new DeactivateFeaturedProductPlacementCommand(id));
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        await Sender.Send(new DeleteFeaturedProductPlacementCommand(id));
        return NoContent();
    }
}

