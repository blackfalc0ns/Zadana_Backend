using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Domain.Modules.Social.Entities;
using Zadana.Infrastructure.Persistence;

namespace Zadana.Infrastructure.Modules.Vendors.Services;

public class VendorReviewAuditService : IVendorReviewAuditService
{
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

        _dbContext.Notifications.Add(new Notification(
            vendorUserId,
            resolvedAuthorName,
            message,
            $"vendor-review|{kind}|{tone}|{roleLabel}"));

        await _dbContext.SaveChangesAsync(cancellationToken);
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
