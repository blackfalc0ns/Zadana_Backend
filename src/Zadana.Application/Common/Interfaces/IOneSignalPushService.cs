namespace Zadana.Application.Common.Interfaces;

public interface IOneSignalPushService
{
    Task<OneSignalPushDispatchResult> SendToExternalUserAsync(
        string externalUserId,
        string titleAr,
        string titleEn,
        string bodyAr,
        string bodyEn,
        string? type = null,
        Guid? referenceId = null,
        string? data = null,
        string? targetUrl = null,
        CancellationToken cancellationToken = default);
}

public sealed record OneSignalPushDispatchResult(
    bool Attempted,
    bool Sent,
    bool Skipped,
    int? ProviderStatusCode,
    string? ProviderNotificationId,
    string? Reason);
