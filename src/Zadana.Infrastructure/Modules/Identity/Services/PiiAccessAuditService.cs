using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Infrastructure.Persistence;

namespace Zadana.Infrastructure.Modules.Identity.Services;

/// <summary>
/// Persists PII access events to <see cref="AccessAuditLog"/> so we can
/// answer "who looked at this field, when, and from where" later.
///
/// We piggy-back on the existing AccessAuditLog table (which already has
/// actor / target / IP / UA fields). The "TargetUserId" column is reused
/// to point at the owning user — the entityId we expose here is logged in
/// the Summary to keep the schema unchanged.
/// </summary>
public sealed class PiiAccessAuditService : IPiiAccessAuditService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<PiiAccessAuditService> _logger;

    public PiiAccessAuditService(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IHttpContextAccessor httpContextAccessor,
        ILogger<PiiAccessAuditService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task RecordAsync(
        string operation,
        string entityType,
        Guid entityId,
        string fieldName,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var actor = _currentUser.UserId;
            var http = _httpContextAccessor.HttpContext;
            var ip = http?.Connection.RemoteIpAddress?.ToString();
            var ua = http?.Request.Headers.UserAgent.ToString();

            var summary = string.IsNullOrWhiteSpace(reason)
                ? $"{operation} {entityType}:{entityId:N}.{fieldName}"
                : $"{operation} {entityType}:{entityId:N}.{fieldName} ({reason})";

            // The legacy schema requires a TargetUserId; for non-user PII (e.g.,
            // a Vendor's IBAN) we record the entityId itself when no real user
            // is available. The Summary keeps the precise context for forensics.
            var entry = new AccessAuditLog(
                actorUserId: actor,
                targetUserId: entityId,
                action: $"PII_{operation}".ToUpperInvariant(),
                summary: summary,
                ipAddress: ip,
                userAgent: ua);

            _db.AccessAuditLogs.Add(entry);
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Audit failures must never break the read; surface to the
            // structured logger instead.
            _logger.LogWarning(ex,
                "Failed to record PII access audit for {Entity}:{EntityId}.{Field}",
                entityType, entityId, fieldName);
        }
    }
}
