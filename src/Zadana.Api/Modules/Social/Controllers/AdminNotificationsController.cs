using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zadana.Api.Controllers;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Social.Commands;
using Zadana.Application.Modules.Social.Queries;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Social.Enums;
using Zadana.Domain.Modules.Social.Support;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Social.Controllers;

[Route("api/admin/notifications")]
[Tags("Admin Dashboard API")]
[Authorize(Policy = "AdminOnly")]
public class AdminNotificationsController : ApiControllerBase
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IApplicationDbContext _context;

    public AdminNotificationsController(
        ICurrentUserService currentUserService,
        IApplicationDbContext context)
    {
        _currentUserService = currentUserService;
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<NotificationsResponse>> GetNotifications(
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
        return Ok(new { message_ar = LocalizedMessages.GetAr(LocalizedMessages.NotificationMarkedRead), message_en = LocalizedMessages.GetEn(LocalizedMessages.NotificationMarkedRead) });
    }

    [HttpPost("read-all")]
    public async Task<ActionResult> MarkAllAsRead(CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var count = await Sender.Send(new MarkAllNotificationsReadCommand(userId), cancellationToken);
        return Ok(new { message_ar = LocalizedMessages.GetAr(LocalizedMessages.AllNotificationsMarkedRead), message_en = LocalizedMessages.GetEn(LocalizedMessages.AllNotificationsMarkedRead), count });
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
        return Ok(new { count, message_ar = "حذفنا جميع الإشعارات", message_en = "All notifications deleted" });
    }

    [HttpGet("preferences")]
    public async Task<ActionResult<AdminNotificationPreferencesResponse>> GetPreferences(CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var devices = await _context.UserPushDevices
            .AsNoTracking()
            .Where(item => item.UserId == userId && item.Platform == PushPlatform.Web && item.IsActive)
            .OrderByDescending(item => item.LastSeenAtUtc)
            .ToListAsync(cancellationToken);

        return Ok(AdminNotificationPreferencesResponse.FromDevice(
            devices.FirstOrDefault(),
            devices.Count));
    }

    [HttpPut("preferences")]
    public async Task<ActionResult<AdminNotificationPreferencesResponse>> UpdatePreferences(
        [FromBody] AdminNotificationPreferencesRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var devices = await _context.UserPushDevices
            .Where(item => item.UserId == userId && item.Platform == PushPlatform.Web && item.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var device in devices)
        {
            device.UpdatePushPreferences(
                request.PushEnabled,
                adminDriversPushEnabled: request.Categories.Drivers,
                adminVendorsPushEnabled: request.Categories.Vendors,
                adminCatalogPushEnabled: request.Categories.Catalog,
                adminDisputesPushEnabled: request.Categories.Disputes,
                adminRefundsPushEnabled: request.Categories.Refunds,
                adminSettlementsPushEnabled: request.Categories.Settlements,
                adminSupportPushEnabled: request.Categories.Support,
                adminSystemPushEnabled: request.Categories.System,
                notificationSound: request.Sound);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(devices.Count == 0
            ? new AdminNotificationPreferencesResponse(
                request.PushEnabled,
                request.Categories,
                NotificationSoundCatalog.Normalize(request.Sound),
                true,
                0)
            : AdminNotificationPreferencesResponse.FromDevice(devices.OrderByDescending(item => item.LastSeenAtUtc).First(), devices.Count));
    }

    [HttpPost("test")]
    public async Task<ActionResult<object>> SendTestNotification(
        [FromServices] IAdminAlertService adminAlertService,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var result = await adminAlertService.SendAsync(
            new AdminAlertRequest(
                "admin.test",
                AdminAlertCategories.System,
                AdminAlertPriorities.Normal,
                "اختبار إشعارات المشرف",
                "Admin notification test",
                "هذا إشعار اختبار للتأكد من عمل Inbox وSignalR وOneSignal.",
                "This is a test notification for Inbox, SignalR, and OneSignal.",
                userId,
                "/notifications",
                new { requestedBy = userId }),
            cancellationToken);

        return Ok(new { result.EventId, result.Status });
    }

    [HttpGet("dispatch-health")]
    public async Task<ActionResult<AdminNotificationDispatchHealthResponse>> GetDispatchHealth(CancellationToken cancellationToken = default)
    {
        var pending = await _context.AdminAlertEvents.CountAsync(item => item.Status == AdminAlertEventStatus.Pending, cancellationToken);
        var processing = await _context.AdminAlertEvents.CountAsync(item => item.Status == AdminAlertEventStatus.Processing, cancellationToken);
        var failedRetryable = await _context.AdminAlertEvents.CountAsync(item => item.Status == AdminAlertEventStatus.FailedRetryable, cancellationToken);
        var deadLetter = await _context.AdminAlertEvents.CountAsync(item => item.Status == AdminAlertEventStatus.DeadLetter, cancellationToken);
        var lastSuccess = await _context.AdminAlertEvents
            .Where(item => item.Status == AdminAlertEventStatus.Completed)
            .MaxAsync(item => (DateTime?)item.CompletedAtUtc, cancellationToken);
        var lastFailure = await _context.AdminAlertEvents
            .Where(item => item.Status == AdminAlertEventStatus.FailedRetryable || item.Status == AdminAlertEventStatus.DeadLetter)
            .MaxAsync(item => (DateTime?)item.LastAttemptAtUtc, cancellationToken);

        return Ok(new AdminNotificationDispatchHealthResponse(pending, processing, failedRetryable, deadLetter, lastSuccess, lastFailure));
    }

    private static NotificationResponse MapNotification(NotificationDto dto) =>
        new(dto.Id, dto.TitleAr, dto.TitleEn, dto.BodyAr, dto.BodyEn,
            dto.Type, dto.Category, dto.Priority, dto.ReferenceId, dto.Data, dto.DataObject, dto.IsRead, dto.CreatedAtUtc);
}

public sealed record AdminNotificationCategoryPreferences(
    bool Drivers = true,
    bool Vendors = true,
    bool Catalog = true,
    bool Disputes = true,
    bool Refunds = true,
    bool Settlements = true,
    bool Support = true,
    bool System = true);

public sealed record AdminNotificationPreferencesRequest(
    bool PushEnabled,
    AdminNotificationCategoryPreferences Categories,
    string? Sound = null);

public sealed record AdminNotificationPreferencesResponse(
    bool PushEnabled,
    AdminNotificationCategoryPreferences Categories,
    string Sound,
    bool CriticalAlwaysOn,
    int WebDeviceCount)
{
    public static AdminNotificationPreferencesResponse FromDevice(
        Domain.Modules.Identity.Entities.UserPushDevice? device,
        int webDeviceCount = 0) =>
        new(
            device?.NotificationsEnabled ?? true,
            new AdminNotificationCategoryPreferences(
                device?.AdminDriversPushEnabled ?? true,
                device?.AdminVendorsPushEnabled ?? true,
                device?.AdminCatalogPushEnabled ?? true,
                device?.AdminDisputesPushEnabled ?? true,
                device?.AdminRefundsPushEnabled ?? true,
                device?.AdminSettlementsPushEnabled ?? true,
                device?.AdminSupportPushEnabled ?? true,
                device?.AdminSystemPushEnabled ?? true),
            NotificationSoundCatalog.Normalize(device?.NotificationSound),
            true,
            webDeviceCount);
}

public sealed record AdminNotificationDispatchHealthResponse(
    int Pending,
    int Processing,
    int FailedRetryable,
    int DeadLetter,
    DateTime? LastSuccessAtUtc,
    DateTime? LastFailureAtUtc);
