using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Infrastructure.Services;

public sealed class AdminAlertService : IAdminAlertService
{
    private readonly IApplicationDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly IOneSignalPushService _oneSignalPushService;
    private readonly ILogger<AdminAlertService> _logger;

    public AdminAlertService(
        IApplicationDbContext context,
        INotificationService notificationService,
        IOneSignalPushService oneSignalPushService,
        ILogger<AdminAlertService> logger)
    {
        _context = context;
        _notificationService = notificationService;
        _oneSignalPushService = oneSignalPushService;
        _logger = logger;
    }

    public async Task<AdminAlertDispatchResult> SendAsync(
        AdminAlertRequest request,
        CancellationToken cancellationToken = default)
    {
        var sanitized = Normalize(request);
        var recipients = await _context.Users
            .AsNoTracking()
            .Where(user =>
                user.AccountStatus == AccountStatus.Active &&
                !user.IsLoginLocked &&
                (user.Role == UserRole.Admin || user.Role == UserRole.SuperAdmin))
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);

        if (recipients.Count == 0)
        {
            _logger.LogWarning(
                "Admin alert {AlertType} skipped because no active admin recipients were found.",
                sanitized.Type);

            return new AdminAlertDispatchResult(
                0,
                0,
                new OneSignalPushDispatchResult(false, false, true, null, null, "No active admin recipients."));
        }

        var data = BuildDataJson(sanitized);
        var signalRSuccessCount = 0;

        foreach (var recipientId in recipients)
        {
            try
            {
                await _notificationService.SendToUserAsync(
                    recipientId,
                    new NotificationDispatchRequest(
                        sanitized.TitleAr,
                        sanitized.TitleEn,
                        sanitized.BodyAr,
                        sanitized.BodyEn,
                        sanitized.Type,
                        sanitized.Category,
                        sanitized.Priority,
                        sanitized.ReferenceId,
                        data),
                    cancellationToken);

                signalRSuccessCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Admin alert {AlertType} failed for admin recipient {RecipientId}.",
                    sanitized.Type,
                    recipientId);
            }
        }

        var pushResults = await _oneSignalPushService.SendToExternalUsersAsync(
            recipients.Select(id => id.ToString()).ToArray(),
            sanitized.TitleAr,
            sanitized.TitleEn,
            sanitized.BodyAr,
            sanitized.BodyEn,
            sanitized.Type,
            sanitized.ReferenceId,
            data,
            sanitized.TargetUrl,
            OneSignalPushProfile.Default,
            OneSignalApplicationTarget.AdminWeb,
            cancellationToken);

        var pushResult = SummarizePushResults(pushResults);

        _logger.LogInformation(
            "Admin alert {AlertType} dispatched. Recipients: {RecipientCount}. SignalRSuccess: {SignalRSuccessCount}. PushAttempted: {PushAttempted}. PushSent: {PushSent}. PushSkipped: {PushSkipped}.",
            sanitized.Type,
            recipients.Count,
            signalRSuccessCount,
            pushResult.Attempted,
            pushResult.Sent,
            pushResult.Skipped);

        return new AdminAlertDispatchResult(recipients.Count, signalRSuccessCount, pushResult);
    }

    private static AdminAlertRequest Normalize(AdminAlertRequest request)
    {
        var type = NormalizeRequired(request.Type, "admin.alert");
        var category = NormalizeRequired(request.Category, AdminAlertCategories.System);
        var priority = NormalizeRequired(request.Priority, AdminAlertPriorities.Normal);
        var targetUrl = string.IsNullOrWhiteSpace(request.TargetUrl) ? "/notifications" : request.TargetUrl.Trim();

        return request with
        {
            Type = type,
            Category = category,
            Priority = priority,
            TitleAr = NormalizeRequired(request.TitleAr, request.TitleEn),
            TitleEn = NormalizeRequired(request.TitleEn, request.TitleAr),
            BodyAr = NormalizeRequired(request.BodyAr, request.BodyEn),
            BodyEn = NormalizeRequired(request.BodyEn, request.BodyAr),
            TargetUrl = targetUrl
        };
    }

    private static string NormalizeRequired(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback.Trim() : value.Trim();

    private static string BuildDataJson(AdminAlertRequest request)
    {
        var envelope = new Dictionary<string, object?>
        {
            ["targetUrl"] = request.TargetUrl,
            ["category"] = request.Category,
            ["priority"] = request.Priority,
            ["source"] = "admin_alert_service"
        };

        if (request.Data is not null)
        {
            envelope["payload"] = request.Data;
        }

        return JsonSerializer.Serialize(envelope);
    }

    private static OneSignalPushDispatchResult SummarizePushResults(IReadOnlyList<OneSignalPushDispatchResult> results)
    {
        if (results.Count == 0)
        {
            return new OneSignalPushDispatchResult(false, false, true, null, null, "No OneSignal push batches were produced.");
        }

        var attempted = results.Any(result => result.Attempted);
        var sent = results.Any(result => result.Sent);
        var skipped = results.All(result => result.Skipped);
        var firstStatus = results.FirstOrDefault(result => result.ProviderStatusCode.HasValue)?.ProviderStatusCode;
        var providerId = results.FirstOrDefault(result => !string.IsNullOrWhiteSpace(result.ProviderNotificationId))?.ProviderNotificationId;
        var reason = results.FirstOrDefault(result => !string.IsNullOrWhiteSpace(result.Reason))?.Reason;

        return new OneSignalPushDispatchResult(attempted, sent, skipped, firstStatus, providerId, reason);
    }
}
