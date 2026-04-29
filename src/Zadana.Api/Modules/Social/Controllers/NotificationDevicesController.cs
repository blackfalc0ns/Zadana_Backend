using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Social.Requests;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Social.Commands;
using Zadana.Application.Modules.Social.Queries;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Social.Controllers;

[Route("api/notifications/devices")]
[Tags("Mobile App API")]
[Authorize]
public class NotificationDevicesController : ApiControllerBase
{
    private readonly ICurrentUserService _currentUserService;

    public NotificationDevicesController(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<ActionResult<NotificationDevicesResponse>> GetDevices(CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        var devices = await Sender.Send(new GetNotificationDevicesQuery(userId), cancellationToken);
        return Ok(new NotificationDevicesResponse(devices.Select(Map).ToList()));
    }

    [HttpPost("register")]
    public async Task<ActionResult<NotificationDeviceResponse>> Register(
        [FromBody] RegisterNotificationDeviceRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        var device = await Sender.Send(new RegisterNotificationDeviceCommand(
            userId,
            request.DeviceToken,
            request.Platform,
            request.DeviceId,
            request.DeviceName,
            request.AppVersion,
            request.Locale,
            request.NotificationsEnabled), cancellationToken);

        return Ok(Map(device));
    }

    [HttpPut("preferences")]
    public async Task<ActionResult<NotificationDeviceResponse>> UpdatePreferences(
        [FromBody] UpdateNotificationDevicePreferencesRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        var device = await Sender.Send(new UpdateNotificationDevicePreferencesCommand(
            userId,
            request.DeviceId,
            request.DeviceToken,
            request.NotificationsEnabled), cancellationToken);

        return Ok(Map(device));
    }

    [HttpPost("unregister")]
    public async Task<ActionResult<NotificationDeviceUnregisterResponse>> Unregister(
        [FromBody] UnregisterNotificationDeviceRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        var count = await Sender.Send(new UnregisterNotificationDeviceCommand(userId, request.DeviceId, request.DeviceToken), cancellationToken);
        return Ok(new NotificationDeviceUnregisterResponse(count));
    }

    private Guid RequireUserId() =>
        _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");

    private static NotificationDeviceResponse Map(NotificationDeviceDto dto) =>
        new(
            dto.Id,
            dto.DeviceToken,
            dto.Platform,
            dto.DeviceId,
            dto.DeviceName,
            dto.AppVersion,
            dto.Locale,
            dto.NotificationsEnabled,
            dto.IsActive,
            dto.LastRegisteredAtUtc,
            dto.LastSeenAtUtc);
}

public record NotificationDevicesResponse(List<NotificationDeviceResponse> Items);

public record NotificationDeviceResponse(
    Guid Id,
    string DeviceToken,
    string Platform,
    string? DeviceId,
    string? DeviceName,
    string? AppVersion,
    string? Locale,
    bool NotificationsEnabled,
    bool IsActive,
    DateTime LastRegisteredAtUtc,
    DateTime LastSeenAtUtc);

public record NotificationDeviceUnregisterResponse(int Count);
