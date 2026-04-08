using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Marketing.Requests;
using Zadana.Application.Modules.Marketing.Commands.HomeContentSections;
using Zadana.Application.Modules.Marketing.DTOs;
using Zadana.Application.Modules.Marketing.Queries.HomeContentSections;

namespace Zadana.Api.Modules.Marketing.Controllers;

[Route("api/admin/marketing/home-content-sections")]
[Authorize(Policy = "AdminOnly")]
[Tags("Marketing (Admins)")]
public class AdminMarketingHomeContentSectionsController : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<HomeContentSectionSettingDto>>> GetSettings()
    {
        var result = await Sender.Send(new GetHomeContentSectionSettingsQuery());
        return Ok(result);
    }

    [HttpPut("{sectionType}")]
    public async Task<ActionResult<HomeContentSectionSettingDto>> UpdateSetting(
        string sectionType,
        [FromBody] UpdateHomeContentSectionSettingRequest request)
    {
        var result = await Sender.Send(new UpdateHomeContentSectionSettingCommand(sectionType, request.IsEnabled));
        return Ok(result);
    }

    [HttpPatch("{sectionType}/activate")]
    public async Task<ActionResult> Activate(string sectionType)
    {
        await Sender.Send(new ActivateHomeContentSectionSettingCommand(sectionType));
        return NoContent();
    }

    [HttpPatch("{sectionType}/deactivate")]
    public async Task<ActionResult> Deactivate(string sectionType)
    {
        await Sender.Send(new DeactivateHomeContentSectionSettingCommand(sectionType));
        return NoContent();
    }
}
