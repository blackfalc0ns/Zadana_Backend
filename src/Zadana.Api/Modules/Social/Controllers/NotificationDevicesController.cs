using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Social.Requests;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Social.Commands;
using Zadana.Application.Modules.Social.Queries;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Social.Controllers;

[Route("api/notifications/devices")]
[Route("api/drivers/notifications/devices")]
[Route("api/admin/notifications/devices")]
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
        var pushToken = FirstNonEmpty(
            request.OneSignalSubscriptionId,
            request.SubscriptionId,
            request.OneSignalId,
            request.DeviceToken);
        var device = await Sender.Send(new RegisterNotificationDeviceCommand(
            userId,
            pushToken,
            request.Platform,
            request.DeviceId,
            request.DeviceName,
            request.AppVersion,
            request.Locale,
            request.NotificationsEnabled,
            request.DispatchPushEnabled,
            request.AssignmentPushEnabled,
            request.SupportPushEnabled,
            request.WalletPushEnabled,
            request.AccountPushEnabled,
            request.AdminDriversPushEnabled,
            request.AdminVendorsPushEnabled,
            request.AdminCatalogPushEnabled,
            request.AdminDisputesPushEnabled,
            request.AdminRefundsPushEnabled,
            request.AdminSettlementsPushEnabled,
            request.AdminSupportPushEnabled,
            request.AdminSystemPushEnabled,
            request.NotificationSound,
            request.CategoryNotificationSounds), cancellationToken);

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
            request.NotificationsEnabled,
            request.DispatchPushEnabled,
            request.AssignmentPushEnabled,
            request.SupportPushEnabled,
            request.WalletPushEnabled,
            request.AccountPushEnabled,
            request.AdminDriversPushEnabled,
            request.AdminVendorsPushEnabled,
            request.AdminCatalogPushEnabled,
            request.AdminDisputesPushEnabled,
            request.AdminRefundsPushEnabled,
            request.AdminSettlementsPushEnabled,
            request.AdminSupportPushEnabled,
            request.AdminSystemPushEnabled,
            request.NotificationSound,
            request.CategoryNotificationSounds), cancellationToken);

        return Ok(Map(device));
    }

    [HttpGet("preferences")]
    public async Task<ActionResult<NotificationDeviceResponse>> GetPreferences(
        [FromQuery(Name = "deviceId")] string? deviceId = null,
        [FromQuery(Name = "deviceToken")] string? deviceToken = null,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        var device = await Sender.Send(
            new GetNotificationDevicePreferencesQuery(userId, deviceId, deviceToken),
            cancellationToken);

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

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

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
            dto.DispatchPushEnabled,
            dto.AssignmentPushEnabled,
            dto.SupportPushEnabled,
            dto.WalletPushEnabled,
            dto.AccountPushEnabled,
            dto.AdminDriversPushEnabled,
            dto.AdminVendorsPushEnabled,
            dto.AdminCatalogPushEnabled,
            dto.AdminDisputesPushEnabled,
            dto.AdminRefundsPushEnabled,
            dto.AdminSettlementsPushEnabled,
            dto.AdminSupportPushEnabled,
            dto.AdminSystemPushEnabled,
            dto.NotificationSound,
            dto.NotificationSounds,
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
    bool DispatchPushEnabled,
    bool AssignmentPushEnabled,
    bool SupportPushEnabled,
    bool WalletPushEnabled,
    bool AccountPushEnabled,
    bool AdminDriversPushEnabled,
    bool AdminVendorsPushEnabled,
    bool AdminCatalogPushEnabled,
    bool AdminDisputesPushEnabled,
    bool AdminRefundsPushEnabled,
    bool AdminSettlementsPushEnabled,
    bool AdminSupportPushEnabled,
    bool AdminSystemPushEnabled,
    string NotificationSound,
    [property: JsonPropertyName("notificationSounds")] IReadOnlyDictionary<string, string> NotificationSounds,
    bool IsActive,
    DateTime LastRegisteredAtUtc,
    DateTime LastSeenAtUtc);

public record NotificationDeviceUnregisterResponse(int Count);
