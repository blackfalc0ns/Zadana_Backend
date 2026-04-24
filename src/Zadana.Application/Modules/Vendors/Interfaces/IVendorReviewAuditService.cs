namespace Zadana.Application.Modules.Vendors.Interfaces;

public interface IVendorReviewAuditService
{
    Task AppendEntryAsync(
        Guid vendorUserId,
        string kind,
        string tone,
        string message,
        string roleLabel,
        string fallbackAuthorName,
        Guid? actorUserId = null,
        string? authorName = null,
        CancellationToken cancellationToken = default);

    Task AppendActivityEntryAsync(
        Guid vendorUserId,
        string kind,
        string severity,
        string message,
        string roleLabel,
        string fallbackAuthorName,
        Guid? actorUserId = null,
        string? authorName = null,
        CancellationToken cancellationToken = default);
}
