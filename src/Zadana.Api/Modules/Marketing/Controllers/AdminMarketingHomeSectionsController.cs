using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Marketing.Requests;
using Zadana.Application.Modules.Marketing.Commands.HomeSections;
using Zadana.Application.Modules.Marketing.DTOs;
using Zadana.Application.Modules.Marketing.Queries.HomeSections;
using Zadana.Domain.Modules.Marketing.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Marketing.Controllers;

[Route("api/admin/marketing/home-sections")]
[Authorize(Policy = "AdminOnly")]
[Tags("Marketing (Admins)")]
public class AdminMarketingHomeSectionsController : ApiControllerBase
{
    [HttpGet("themes")]
    public ActionResult<List<HomeSectionThemeOptionResponse>> GetThemes()
    {
        var result = HomeSectionThemeCatalog.All
            .Select(theme => new HomeSectionThemeOptionResponse(
                theme.ToKey(),
                theme.ToArabicLabel(),
                theme.ToEnglishLabel()))
            .ToList();

        return Ok(result);
    }

    [HttpGet]
    public async Task<ActionResult<List<HomeSectionAdminDto>>> GetSections()
    {
        var result = await Sender.Send(new GetHomeSectionsQuery());
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<HomeSectionAdminDto>> GetSection(Guid id)
    {
        var result = await Sender.Send(new GetHomeSectionByIdQuery(id));
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<HomeSectionAdminDto>> CreateSection([FromBody] CreateHomeSectionRequest request)
    {
        if (!HomeSectionThemeCatalog.TryParseKey(request.Theme, out var theme))
        {
            throw new BadRequestException("INVALID_HOME_SECTION_THEME", "Theme is invalid.");
        }

        var result = await Sender.Send(new CreateHomeSectionCommand(
            request.CategoryId,
            theme,
            request.DisplayOrder,
            request.ProductsTake,
            request.StartsAtUtc,
            request.EndsAtUtc));

        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<HomeSectionAdminDto>> UpdateSection(Guid id, [FromBody] UpdateHomeSectionRequest request)
    {
        if (!HomeSectionThemeCatalog.TryParseKey(request.Theme, out var theme))
        {
            throw new BadRequestException("INVALID_HOME_SECTION_THEME", "Theme is invalid.");
        }

        var result = await Sender.Send(new UpdateHomeSectionCommand(
            id,
            request.CategoryId,
            theme,
            request.DisplayOrder,
            request.ProductsTake,
            request.StartsAtUtc,
            request.EndsAtUtc,
            request.IsActive));

        return Ok(result);
    }

    [HttpPatch("{id:guid}/activate")]
    public async Task<ActionResult> Activate(Guid id)
    {
        await Sender.Send(new ActivateHomeSectionCommand(id));
        return NoContent();
    }

    [HttpPatch("{id:guid}/deactivate")]
    public async Task<ActionResult> Deactivate(Guid id)
    {
        await Sender.Send(new DeactivateHomeSectionCommand(id));
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        await Sender.Send(new DeleteHomeSectionCommand(id));
        return NoContent();
    }
}
