using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zadana.Api.Controllers;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Social.Commands;
using Zadana.Application.Modules.Social.Queries;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Social.Support;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Delivery.Controllers;

[Route("api/drivers/notifications")]
[Tags("Driver App API")]
[Authorize(Policy = "DriverOnly")]
public class DriverNotificationsController : ApiControllerBase
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IApplicationDbContext _context;

    public DriverNotificationsController(
        ICurrentUserService currentUserService,
        IApplicationDbContext context)
    {
        _currentUserService = currentUserService;
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<DriverNotificationsResponse>> GetNotifications(
        [FromQuery(Name = "page")] int page = 1,
        [FromQuery(Name = "per_page")] int perPage = 20,
        [FromQuery(Name = "type")] string? type = null,
        [FromQuery(Name = "category")] string? category = null,
        [FromQuery(Name = "priority")] string? priority = null,
        [FromQuery(Name = "is_read")] bool? isRead = null,
        [FromQuery(Name = "from_utc")] DateTime? fromUtc = null,
        [FromQuery(Name = "to_utc")] DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var result = await Sender.Send(
            new GetNotificationsQuery(userId, page, perPage, type, isRead, fromUtc, toUtc, category, priority),
            cancellationToken);

        return Ok(new DriverNotificationsResponse(
            result.Items.Select(MapNotification).ToList(),
            result.Page,
            result.PerPage,
            result.Total,
            result.UnreadCount,
            result.HasMore));
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<DriverUnreadCountResponse>> GetUnreadCount(CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var count = await Sender.Send(new GetUnreadNotificationCountQuery(userId), cancellationToken);
        return Ok(new DriverUnreadCountResponse(count));
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        await Sender.Send(new MarkNotificationReadCommand(id, userId), cancellationToken);
        return Ok(new { message_ar = LocalizedMessages.GetAr(LocalizedMessages.NotificationMarkedRead), message_en = LocalizedMessages.GetEn(LocalizedMessages.NotificationMarkedRead) });
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var count = await Sender.Send(new MarkAllNotificationsReadCommand(userId), cancellationToken);
        return Ok(new { message_ar = LocalizedMessages.GetAr(LocalizedMessages.AllNotificationsMarkedRead), message_en = LocalizedMessages.GetEn(LocalizedMessages.AllNotificationsMarkedRead), count });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteNotification(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        await Sender.Send(new DeleteNotificationCommand(id, userId), cancellationToken);
        return NoContent();
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteAllNotifications(CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var count = await Sender.Send(new DeleteAllNotificationsCommand(userId), cancellationToken);
        return Ok(new { count, message_ar = "تم حذف جميع الإشعارات", message_en = "All notifications deleted" });
    }

    [HttpGet("preferences")]
    public async Task<ActionResult<DriverNotificationPreferencesResponse>> GetPreferences(
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var device = await _context.UserPushDevices
            .AsNoTracking()
            .Where(d => d.UserId == userId && d.IsActive)
            .OrderByDescending(d => d.LastSeenAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(DriverNotificationPreferencesResponse.FromDevice(device));
    }

    [HttpPut("preferences")]
    public async Task<ActionResult<DriverNotificationPreferencesResponse>> UpdatePreferences(
        [FromBody] DriverNotificationPreferencesRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var devices = await _context.UserPushDevices
            .Where(d => d.UserId == userId && d.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var device in devices)
        {
            device.UpdatePushPreferences(
                request.PushEnabled,
                notificationSound: request.Sound);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(devices.Count == 0
            ? new DriverNotificationPreferencesResponse(request.PushEnabled, NotificationSoundCatalog.Normalize(request.Sound), 0)
            : DriverNotificationPreferencesResponse.FromDevice(devices.OrderByDescending(d => d.LastSeenAtUtc).First(), devices.Count));
    }

    private static DriverNotificationResponse MapNotification(NotificationDto dto) =>
        new(dto.Id, dto.TitleAr, dto.TitleEn, dto.BodyAr, dto.BodyEn,
            dto.Type, dto.Category, dto.Priority, dto.ReferenceId, dto.Data, dto.DataObject, dto.IsRead, dto.CreatedAtUtc);
}

public record DriverNotificationsResponse(
    List<DriverNotificationResponse> Items,
    int Page,
    int PerPage,
    int Total,
    int UnreadCount,
    bool HasMore);

public record DriverNotificationResponse(
    Guid Id,
    string TitleAr,
    string TitleEn,
    string BodyAr,
    string BodyEn,
    string? Type,
    string? Category,
    string? Priority,
    Guid? ReferenceId,
    string? Data,
    JsonElement? DataObject,
    bool IsRead,
    DateTime CreatedAtUtc);

public record DriverUnreadCountResponse(int Count);

public sealed record DriverNotificationPreferencesRequest(
    bool PushEnabled,
    string? Sound = null);

public sealed record DriverNotificationPreferencesResponse(
    bool PushEnabled,
    string Sound,
    int MobileDeviceCount)
{
    public static DriverNotificationPreferencesResponse FromDevice(
        Domain.Modules.Identity.Entities.UserPushDevice? device,
        int deviceCount = 0) =>
        new(
            device?.NotificationsEnabled ?? true,
            NotificationSoundCatalog.Normalize(device?.NotificationSound),
            deviceCount);
}
