using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Zadana.Api.Authorization;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Marketing.Requests;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Identity.Constants;
using Zadana.Domain.Modules.Marketing.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Marketing.Controllers;

[Route("api/admin/marketing/platform-contact")]
[Authorize(Policy = "AdminOnly")]
[Tags("Marketing (Admins)")]
public sealed class AdminMarketingPlatformContactController : ApiControllerBase
{
    [HttpGet]
    [RequireAccess(PermissionKeys.Admin.MarketingView)]
    public async Task<ActionResult<PlatformContactSettingsDto>> Get(
        [FromServices] IApplicationDbContext context,
        [FromServices] IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var settings = await context.PlatformContactSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == PlatformContactSettings.SingletonId, cancellationToken);

        return Ok(settings is null
            ? BuildFallback(configuration)
            : Map(settings));
    }

    [HttpPut]
    [RequireAccess(PermissionKeys.Admin.MarketingManageSettings)]
    public async Task<ActionResult<PlatformContactSettingsDto>> Upsert(
        [FromBody] UpsertPlatformContactSettingsRequest request,
        [FromServices] IApplicationDbContext context,
        [FromServices] ICurrentUserService currentUserService,
        CancellationToken cancellationToken)
    {
        var actorId = currentUserService.UserId
            ?? throw new UnauthorizedException("ADMIN_NOT_AUTHENTICATED");

        var settings = await context.PlatformContactSettings
            .FirstOrDefaultAsync(item => item.Id == PlatformContactSettings.SingletonId, cancellationToken);

        if (settings is null)
        {
            settings = new PlatformContactSettings(actorId);
            context.PlatformContactSettings.Add(settings);
        }

        settings.Update(
            request.SupportEmail,
            request.SupportPhone,
            request.WhatsAppUrl,
            request.InstagramUrl,
            request.TwitterUrl,
            request.TikTokUrl,
            request.SnapchatUrl,
            request.FacebookUrl,
            request.YouTubeUrl,
            request.LinkedInUrl,
            actorId);

        await context.SaveChangesAsync(cancellationToken);
        return Ok(Map(settings));
    }

    private static PlatformContactSettingsDto BuildFallback(IConfiguration configuration) =>
        new(
            configuration["Email:SupportEmail"],
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

    private static PlatformContactSettingsDto Map(PlatformContactSettings settings) =>
        new(
            settings.SupportEmail,
            settings.SupportPhone,
            settings.WhatsAppUrl,
            settings.InstagramUrl,
            settings.TwitterUrl,
            settings.TikTokUrl,
            settings.SnapchatUrl,
            settings.FacebookUrl,
            settings.YouTubeUrl,
            settings.LinkedInUrl,
            settings.UpdatedAtUtc);
}

[Route("api/public/platform-contact")]
[AllowAnonymous]
[Tags("Public Platform Contact")]
public sealed class PublicPlatformContactController : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PlatformContactSettingsDto>> Get(
        [FromServices] IApplicationDbContext context,
        [FromServices] IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var settings = await context.PlatformContactSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == PlatformContactSettings.SingletonId, cancellationToken);

        if (settings is null)
        {
            return Ok(new PlatformContactSettingsDto(
                configuration["Email:SupportEmail"],
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null));
        }

        return Ok(new PlatformContactSettingsDto(
            settings.SupportEmail,
            settings.SupportPhone,
            settings.WhatsAppUrl,
            settings.InstagramUrl,
            settings.TwitterUrl,
            settings.TikTokUrl,
            settings.SnapchatUrl,
            settings.FacebookUrl,
            settings.YouTubeUrl,
            settings.LinkedInUrl,
            settings.UpdatedAtUtc));
    }
}
