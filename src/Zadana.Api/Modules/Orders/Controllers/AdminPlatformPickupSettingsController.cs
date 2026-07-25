using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zadana.Api.Authorization;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Orders.Requests;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Identity.Constants;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Orders.Controllers;

[Route("api/admin/orders/pickup-settings")]
[Authorize(Policy = "AdminOnly")]
[Tags("Admin Dashboard API")]
public sealed class AdminPlatformPickupSettingsController : ApiControllerBase
{
    [HttpGet]
    [RequireAccess(PermissionKeys.Admin.OrdersView)]
    public async Task<ActionResult<PlatformPickupSettingsDto>> Get(
        [FromServices] IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var settings = await context.PlatformPickupSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == PlatformPickupSettings.SingletonId, cancellationToken);

        return Ok(settings is null ? BuildDefaultDto() : Map(settings));
    }

    [HttpPut]
    [RequireAccess(PermissionKeys.Admin.SystemManageSettings)]
    public async Task<ActionResult<PlatformPickupSettingsDto>> Upsert(
        [FromBody] UpsertPlatformPickupSettingsRequest request,
        [FromServices] IApplicationDbContext context,
        [FromServices] ICurrentUserService currentUserService,
        CancellationToken cancellationToken)
    {
        var actorId = currentUserService.UserId
            ?? throw new UnauthorizedException("ADMIN_NOT_AUTHENTICATED");

        var settings = await context.PlatformPickupSettings
            .FirstOrDefaultAsync(item => item.Id == PlatformPickupSettings.SingletonId, cancellationToken);

        if (settings is null)
        {
            settings = new PlatformPickupSettings(actorId);
            context.PlatformPickupSettings.Add(settings);
        }

        settings.Update(
            request.DeliveryOptionEnabled,
            request.PickupOptionEnabled,
            request.PickupCashOnPickupEnabled,
            request.PickupCommissionPercent,
            request.PickupNoShowTimeoutHours,
            request.PickupOtpMaxAttempts,
            request.PickupOtpLockoutMinutes,
            actorId);

        await context.SaveChangesAsync(cancellationToken);
        return Ok(Map(settings));
    }

    private static PlatformPickupSettingsDto BuildDefaultDto() =>
        new(
            DeliveryOptionEnabled: true,
            PickupOptionEnabled: true,
            PickupCashOnPickupEnabled: false,
            PickupCommissionPercent: 5.0m,
            PickupNoShowTimeoutHours: 24,
            PickupOtpMaxAttempts: 5,
            PickupOtpLockoutMinutes: 30,
            UpdatedAtUtc: null);

    private static PlatformPickupSettingsDto Map(PlatformPickupSettings settings) =>
        new(
            settings.DeliveryOptionEnabled,
            settings.PickupOptionEnabled,
            settings.PickupCashOnPickupEnabled,
            settings.PickupCommissionPercent,
            settings.PickupNoShowTimeoutHours,
            settings.PickupOtpMaxAttempts,
            settings.PickupOtpLockoutMinutes,
            settings.UpdatedAtUtc);
}
