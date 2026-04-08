using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Marketing.Requests;
using Zadana.Application.Modules.Marketing.Commands.HomeBanners;
using Zadana.Application.Modules.Marketing.DTOs;
using Zadana.Application.Modules.Marketing.Queries.HomeBanners;

namespace Zadana.Api.Modules.Marketing.Controllers;

[Route("api/admin/marketing/banners")]
[Authorize(Policy = "AdminOnly")]
[Tags("Marketing (Admins)")]
public class AdminMarketingBannersController : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<HomeBannerAdminDto>>> GetBanners()
    {
        var result = await Sender.Send(new GetHomeBannersQuery());
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<HomeBannerAdminDto>> GetBanner(Guid id)
    {
        var result = await Sender.Send(new GetHomeBannerByIdQuery(id));
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<HomeBannerAdminDto>> CreateBanner([FromBody] CreateHomeBannerRequest request)
    {
        var result = await Sender.Send(new CreateHomeBannerCommand(
            request.TagAr, request.TagEn, request.TitleAr, request.TitleEn,
            request.SubtitleAr, request.SubtitleEn, request.ActionLabelAr, request.ActionLabelEn,
            request.ImageUrl, request.DisplayOrder, request.StartsAtUtc, request.EndsAtUtc));
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<HomeBannerAdminDto>> UpdateBanner(Guid id, [FromBody] UpdateHomeBannerRequest request)
    {
        var result = await Sender.Send(new UpdateHomeBannerCommand(
            id, request.TagAr, request.TagEn, request.TitleAr, request.TitleEn,
            request.SubtitleAr, request.SubtitleEn, request.ActionLabelAr, request.ActionLabelEn,
            request.ImageUrl, request.DisplayOrder, request.StartsAtUtc, request.EndsAtUtc, request.IsActive));
        return Ok(result);
    }

    [HttpPatch("{id:guid}/activate")]
    public async Task<ActionResult> Activate(Guid id)
    {
        await Sender.Send(new ActivateHomeBannerCommand(id));
        return NoContent();
    }

    [HttpPatch("{id:guid}/deactivate")]
    public async Task<ActionResult> Deactivate(Guid id)
    {
        await Sender.Send(new DeactivateHomeBannerCommand(id));
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        await Sender.Send(new DeleteHomeBannerCommand(id));
        return NoContent();
    }
}

