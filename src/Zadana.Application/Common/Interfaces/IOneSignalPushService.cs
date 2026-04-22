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

    Task<OneSignalPushDispatchResult> SendToExternalUserAsync(
        string externalUserId,
        string titleAr,
        string titleEn,
        string bodyAr,
        string bodyEn,
        string? type,
        Guid? referenceId,
        string? data,
        string? targetUrl,
        OneSignalPushProfile profile,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OneSignalPushDispatchResult>> SendToExternalUsersAsync(
        IReadOnlyCollection<string> externalUserIds,
        string titleAr,
        string titleEn,
        string bodyAr,
        string bodyEn,
        string? type = null,
        Guid? referenceId = null,
        string? data = null,
        string? targetUrl = null,
        OneSignalPushProfile profile = OneSignalPushProfile.Default,
        CancellationToken cancellationToken = default);
}

public enum OneSignalPushProfile
{
    Default = 0,
    MobileHeadsUp = 1,
    MobileOrderUpdates = 2
}

public sealed record OneSignalPushDispatchResult(
    bool Attempted,
    bool Sent,
    bool Skipped,
    int? ProviderStatusCode,
    string? ProviderNotificationId,
    string? Reason);
