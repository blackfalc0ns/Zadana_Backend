using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Social.Requests;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Social.Queries;
using Zadana.SharedKernel.Exceptions;
using System.Text.Json;

namespace Zadana.Api.Modules.Social.Controllers;

[Route("api/vendor/notifications")]
[Tags("Vendor App API")]
[Authorize(Policy = "VendorOnly")]
public class VendorNotificationsController : ApiControllerBase
{
    private readonly ICurrentUserService _currentUserService;
    private readonly INotificationService _notificationService;
    private readonly IOneSignalPushService _oneSignalPushService;

    public VendorNotificationsController(
        ICurrentUserService currentUserService,
        INotificationService notificationService,
        IOneSignalPushService oneSignalPushService)
    {
        _currentUserService = currentUserService;
        _notificationService = notificationService;
        _oneSignalPushService = oneSignalPushService;
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
        return Ok(new { message = "notification marked as read" });
    }

    [HttpPost("read-all")]
    public async Task<ActionResult> MarkAllAsRead(CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var count = await Sender.Send(new MarkAllNotificationsReadCommand(userId), cancellationToken);
        return Ok(new { message = "all notifications marked as read", count });
    }

    [HttpPost("test")]
    public async Task<ActionResult<VendorTestNotificationResponse>> SendTestNotification(
        [FromBody] SendVendorTestNotificationRequest? request,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        request ??= new SendVendorTestNotificationRequest();

        var titleAr = string.IsNullOrWhiteSpace(request.TitleAr) ? "إشعار تجريبي للتاجر" : request.TitleAr.Trim();
        var titleEn = string.IsNullOrWhiteSpace(request.TitleEn) ? "Vendor test notification" : request.TitleEn.Trim();
        var bodyAr = string.IsNullOrWhiteSpace(request.BodyAr)
            ? "هذا إشعار تجريبي للتأكد من ربط لوحة التاجر مع OneSignal."
            : request.BodyAr.Trim();
        var bodyEn = string.IsNullOrWhiteSpace(request.BodyEn)
            ? "This is a test notification to verify your vendor panel is connected to OneSignal."
            : request.BodyEn.Trim();
        var type = string.IsNullOrWhiteSpace(request.Type) ? "vendor_test" : request.Type.Trim();
        var data = string.IsNullOrWhiteSpace(request.Data)
            ? JsonSerializer.Serialize(new
            {
                source = "vendor_notifications_test_api",
                generatedAtUtc = DateTime.UtcNow,
                targetUrl = request.TargetUrl
            })
            : request.Data;

        await _notificationService.SendToUserAsync(
            userId,
            titleAr,
            titleEn,
            bodyAr,
            bodyEn,
            type,
            request.ReferenceId,
            data,
            cancellationToken);

        var pushResult = request.SendPush
            ? await _oneSignalPushService.SendToExternalUserAsync(
                userId.ToString(),
                titleAr,
                titleEn,
                bodyAr,
                bodyEn,
                type,
                request.ReferenceId,
                data,
                request.TargetUrl,
                cancellationToken)
            : new OneSignalPushDispatchResult(
                Attempted: false,
                Sent: false,
                Skipped: true,
                ProviderStatusCode: null,
                ProviderNotificationId: null,
                Reason: "Push dispatch was disabled for this test request.");

        return Ok(new VendorTestNotificationResponse(
            Message: "Vendor test notification queued.",
            UserId: userId,
            ExternalId: userId.ToString(),
            Type: type,
            InboxRequested: true,
            PushAttempted: pushResult.Attempted,
            PushSent: pushResult.Sent,
            PushSkipped: pushResult.Skipped,
            PushStatusCode: pushResult.ProviderStatusCode,
            ProviderNotificationId: pushResult.ProviderNotificationId,
            PushReason: pushResult.Reason));
    }

    private static NotificationResponse MapNotification(NotificationDto dto) =>
        new(dto.Id, dto.TitleAr, dto.TitleEn, dto.BodyAr, dto.BodyEn,
            dto.Type, dto.ReferenceId, dto.Data, dto.DataObject, dto.IsRead, dto.CreatedAtUtc);
}

public record VendorTestNotificationResponse(
    string Message,
    Guid UserId,
    string ExternalId,
    string Type,
    bool InboxRequested,
    bool PushAttempted,
    bool PushSent,
    bool PushSkipped,
    int? PushStatusCode,
    string? ProviderNotificationId,
    string? PushReason);
