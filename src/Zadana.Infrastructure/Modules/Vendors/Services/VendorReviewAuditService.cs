using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Domain.Modules.Social.Entities;
using Zadana.Infrastructure.Persistence;

namespace Zadana.Infrastructure.Modules.Vendors.Services;

public class VendorReviewAuditService : IVendorReviewAuditService
{
    private const string ReviewPrefix = "vendor-review";
    private const string ActivityPrefix = "vendor-activity";
    private readonly ApplicationDbContext _dbContext;
    private readonly IIdentityAccountService _identityAccountService;

    public VendorReviewAuditService(
        ApplicationDbContext dbContext,
        IIdentityAccountService identityAccountService)
    {
        _dbContext = dbContext;
        _identityAccountService = identityAccountService;
    }

    public async Task AppendEntryAsync(
        Guid vendorUserId,
        string kind,
        string tone,
        string message,
        string roleLabel,
        string fallbackAuthorName,
        Guid? actorUserId = null,
        string? authorName = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedAuthorName = string.IsNullOrWhiteSpace(authorName)
            ? await ResolveActorNameAsync(actorUserId, fallbackAuthorName, cancellationToken)
            : authorName.Trim();

        _dbContext.Notifications.Add(CreateAuditNotification(
            vendorUserId,
            resolvedAuthorName,
            message,
            BuildAuditType(ReviewPrefix, kind, tone, roleLabel)));

        _dbContext.Notifications.Add(CreateAuditNotification(
            vendorUserId,
            resolvedAuthorName,
            message,
            BuildAuditType(ActivityPrefix, kind, tone, roleLabel)));

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AppendActivityEntryAsync(
        Guid vendorUserId,
        string kind,
        string severity,
        string message,
        string roleLabel,
        string fallbackAuthorName,
        Guid? actorUserId = null,
        string? authorName = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedAuthorName = string.IsNullOrWhiteSpace(authorName)
            ? await ResolveActorNameAsync(actorUserId, fallbackAuthorName, cancellationToken)
            : authorName.Trim();

        _dbContext.Notifications.Add(CreateAuditNotification(
            vendorUserId,
            resolvedAuthorName,
            message,
            BuildAuditType(ActivityPrefix, kind, severity, roleLabel)));

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static Notification CreateAuditNotification(
        Guid vendorUserId,
        string authorName,
        string message,
        string type) =>
        new(
            vendorUserId,
            authorName,
            authorName,
            message,
            message,
            type);

    private static string BuildAuditType(string prefix, string kind, string severity, string roleLabel)
    {
        static string NormalizePart(string? value, string fallback) =>
            string.IsNullOrWhiteSpace(value)
                ? fallback
                : value.Trim().Replace('|', '/');

        return $"{prefix}|{NormalizePart(kind, "note")}|{NormalizePart(severity, "info")}|{NormalizePart(roleLabel, "Vendor Review")}";
    }

    private async Task<string> ResolveActorNameAsync(Guid? actorUserId, string fallbackAuthorName, CancellationToken cancellationToken)
    {
        if (!actorUserId.HasValue)
        {
            return fallbackAuthorName;
        }

        var actor = await _identityAccountService.FindByIdAsync(actorUserId.Value, cancellationToken);
        return string.IsNullOrWhiteSpace(actor?.FullName) ? fallbackAuthorName : actor.FullName;
    }
}
