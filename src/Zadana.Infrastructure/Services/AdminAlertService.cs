using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Social.Entities;

namespace Zadana.Infrastructure.Services;

public sealed class AdminAlertService : IAdminAlertService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IApplicationDbContext _context;
    private readonly ILogger<AdminAlertService> _logger;

    public AdminAlertService(
        IApplicationDbContext context,
        ILogger<AdminAlertService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<AdminAlertDispatchResult> SendAsync(
        AdminAlertRequest request,
        CancellationToken cancellationToken = default)
    {
        var sanitized = Normalize(request);
        var dataJson = BuildDataJson(sanitized);
        var dedupeKey = BuildDedupeKey(sanitized);
        var dedupeWindow = ResolveDedupeWindow(sanitized);
        var cutoff = DateTime.UtcNow.Subtract(dedupeWindow);

        var existing = await _context.AdminAlertEvents
            .AsNoTracking()
            .Where(item => item.DedupeKey == dedupeKey && item.CreatedAtUtc >= cutoff)
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            _logger.LogInformation(
                "Admin alert {AlertType} deduped into existing event {EventId}.",
                sanitized.Type,
                existing.Id);

            return new AdminAlertDispatchResult(
                0,
                0,
                new OneSignalPushDispatchResult(false, false, true, null, null, "Deduplicated."))
            {
                EventId = existing.Id,
                Status = "deduped"
            };
        }

        var alertEvent = new AdminAlertEvent(
            sanitized.Type,
            sanitized.Category,
            sanitized.Priority,
            sanitized.TitleAr,
            sanitized.TitleEn,
            sanitized.BodyAr,
            sanitized.BodyEn,
            sanitized.ReferenceId,
            sanitized.TargetUrl,
            dataJson,
            dedupeKey,
            sanitized.SuppressPush);

        _context.AdminAlertEvents.Add(alertEvent);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Admin alert {AlertType} queued as outbox event {EventId}.",
            alertEvent.Type,
            alertEvent.Id);

        return new AdminAlertDispatchResult(
            0,
            0,
            new OneSignalPushDispatchResult(false, false, true, null, null, "Queued for outbox dispatch."))
        {
            EventId = alertEvent.Id,
            Status = "queued"
        };
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
            ["source"] = "admin_alert_outbox"
        };

        if (request.Data is not null)
        {
            envelope["payload"] = request.Data;
        }

        return JsonSerializer.Serialize(envelope, JsonOptions);
    }

    private static string BuildDedupeKey(AdminAlertRequest request) =>
        $"{request.Type.Trim().ToLowerInvariant()}|{request.ReferenceId?.ToString("N") ?? "none"}|{request.TargetUrl.Trim().ToLowerInvariant()}";

    private static TimeSpan ResolveDedupeWindow(AdminAlertRequest request) =>
        string.Equals(request.Type, AdminAlertTypes.SystemIntegrationFailure, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(request.Type, AdminAlertTypes.SystemOneSignalFailure, StringComparison.OrdinalIgnoreCase)
            ? TimeSpan.FromMinutes(1)
            : TimeSpan.FromMinutes(5);
}

