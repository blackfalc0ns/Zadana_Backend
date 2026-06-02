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

namespace Zadana.Api.Modules.Social.Controllers;

[Route("api/notifications")]
[Tags("Customer App API")]
[Authorize(Policy = "CustomerOnly")]
public class NotificationsController : ApiControllerBase
{
    private readonly ICurrentUserService _currentUserService;

    public NotificationsController(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<ActionResult<NotificationsResponse>> GetNotifications(
        [FromQuery(Name = "page")] int page = 1,
        [FromQuery(Name = "per_page")] int perPage = 20,
        [FromQuery(Name = "type")] string? type = null,
        [FromQuery(Name = "is_read")] bool? isRead = null,
        [FromQuery(Name = "from_utc")] DateTime? fromUtc = null,
        [FromQuery(Name = "to_utc")] DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var result = await Sender.Send(
            new GetNotificationsQuery(userId, page, perPage, type, isRead, fromUtc, toUtc),
            cancellationToken);

        return Ok(new NotificationsResponse(
            result.Items.Select(MapNotification).ToList(),
            result.Page,
            result.PerPage,
            result.Total,
            result.UnreadCount,
            result.HasMore));
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountResponse>> GetUnreadCount(CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var count = await Sender.Send(new GetUnreadNotificationCountQuery(userId), cancellationToken);
        return Ok(new UnreadCountResponse(count));
    }

    [HttpPost("{id:guid}/read")]
    public async Task<ActionResult> MarkAsRead(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        await Sender.Send(new MarkNotificationReadCommand(id, userId), cancellationToken);
        return Ok(new
        {
            message_ar = LocalizedMessages.GetAr(LocalizedMessages.NotificationMarkedRead),
            message_en = LocalizedMessages.GetEn(LocalizedMessages.NotificationMarkedRead)
        });
    }

    [HttpPost("read-all")]
    public async Task<ActionResult> MarkAllAsRead(CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var count = await Sender.Send(new MarkAllNotificationsReadCommand(userId), cancellationToken);
        return Ok(new
        {
            message_ar = LocalizedMessages.GetAr(LocalizedMessages.AllNotificationsMarkedRead),
            message_en = LocalizedMessages.GetEn(LocalizedMessages.AllNotificationsMarkedRead),
            count
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteNotification(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        await Sender.Send(new DeleteNotificationCommand(id, userId), cancellationToken);
        return NoContent();
    }

    [HttpDelete]
    public async Task<ActionResult> DeleteAllNotifications(CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var count = await Sender.Send(new DeleteAllNotificationsCommand(userId), cancellationToken);
        return Ok(new
        {
            count,
            message_ar = "\u062a\u0645 \u062d\u0630\u0641 \u062c\u0645\u064a\u0639 \u0627\u0644\u0625\u0634\u0639\u0627\u0631\u0627\u062a",
            message_en = "All notifications deleted"
        });
    }

    [HttpGet("preferences")]
    public async Task<ActionResult<CustomerNotificationPreferencesResponse>> GetPreferences(
        [FromServices] IApplicationDbContext context,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var device = await context.UserPushDevices
            .AsNoTracking()
            .Where(d => d.UserId == userId && (d.Platform == PushPlatform.Fcm || d.Platform == PushPlatform.Apns) && d.IsActive)
            .OrderByDescending(d => d.LastSeenAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(CustomerNotificationPreferencesResponse.FromDevice(device));
    }

    [HttpPut("preferences")]
    public async Task<ActionResult<CustomerNotificationPreferencesResponse>> UpdatePreferences(
        [FromBody] CustomerNotificationPreferencesRequest request,
        [FromServices] IApplicationDbContext context,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var devices = await context.UserPushDevices
            .Where(d => d.UserId == userId && (d.Platform == PushPlatform.Fcm || d.Platform == PushPlatform.Apns) && d.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var device in devices)
        {
            device.UpdatePushPreferences(
                request.PushEnabled,
                notificationSound: request.Sound);
        }

        await context.SaveChangesAsync(cancellationToken);

        return Ok(devices.Count == 0
            ? new CustomerNotificationPreferencesResponse(request.PushEnabled, NotificationSoundCatalog.Normalize(request.Sound), 0)
            : CustomerNotificationPreferencesResponse.FromDevice(devices.OrderByDescending(d => d.LastSeenAtUtc).First(), devices.Count));
    }

    private static NotificationResponse MapNotification(NotificationDto dto) =>
        new(dto.Id, dto.TitleAr, dto.TitleEn, dto.BodyAr, dto.BodyEn,
            dto.Type, dto.Category, dto.Priority, dto.ReferenceId, dto.Data, dto.DataObject, dto.IsRead, dto.CreatedAtUtc);
}

public record NotificationsResponse(
    List<NotificationResponse> Items,
    int Page,
    int PerPage,
    int Total,
    int UnreadCount,
    bool HasMore);

public record NotificationResponse(
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

public record UnreadCountResponse(int Count);

public sealed record CustomerNotificationPreferencesRequest(
    bool PushEnabled,
    string? Sound = null);

public sealed record CustomerNotificationPreferencesResponse(
    bool PushEnabled,
    string Sound,
    int MobileDeviceCount)
{
    public static CustomerNotificationPreferencesResponse FromDevice(
        Domain.Modules.Identity.Entities.UserPushDevice? device,
        int deviceCount = 0) =>
        new(
            device?.NotificationsEnabled ?? true,
            NotificationSoundCatalog.Normalize(device?.NotificationSound),
            deviceCount);
}
