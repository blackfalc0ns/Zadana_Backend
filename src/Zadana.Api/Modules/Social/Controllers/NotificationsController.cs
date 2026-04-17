using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Social.Queries;
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
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var result = await Sender.Send(new GetNotificationsQuery(userId, page, perPage), cancellationToken);

        return Ok(new NotificationsResponse(
            result.Items.Select(MapNotification).ToList(),
            result.Page,
            result.PerPage,
            result.Total));
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
        return Ok(new { message = "notification marked as read" });
    }

    [HttpPost("read-all")]
    public async Task<ActionResult> MarkAllAsRead(CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var count = await Sender.Send(new MarkAllNotificationsReadCommand(userId), cancellationToken);
        return Ok(new { message = "all notifications marked as read", count });
    }

    private static NotificationResponse MapNotification(NotificationDto dto) =>
        new(dto.Id, dto.TitleAr, dto.TitleEn, dto.BodyAr, dto.BodyEn,
            dto.Type, dto.ReferenceId, dto.Data, dto.IsRead, dto.CreatedAtUtc);
}

// Response DTOs
public record NotificationsResponse(
    List<NotificationResponse> Items,
    int Page,
    int PerPage,
    int Total);

public record NotificationResponse(
    Guid Id,
    string TitleAr,
    string TitleEn,
    string BodyAr,
    string BodyEn,
    string? Type,
    Guid? ReferenceId,
    string? Data,
    bool IsRead,
    DateTime CreatedAtUtc);

public record UnreadCountResponse(int Count);
