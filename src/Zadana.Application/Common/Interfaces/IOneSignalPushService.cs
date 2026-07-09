namespace Zadana.Application.Common.Interfaces;

public interface IOneSignalPushService
{
    Task<OneSignalPushDispatchResult> SendMobileNotificationAsync(
        OneSignalMobilePushRequest request,
        CancellationToken cancellationToken = default);

    Task<OneSignalPushDispatchResult> SendMobileNotificationDirectAsync(
        OneSignalMobilePushRequest request,
        CancellationToken cancellationToken = default);

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

    async Task<OneSignalPushDispatchResult> SendToExternalUserAsync(
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
        OneSignalApplicationTarget targetApplication,
        CancellationToken cancellationToken = default)
    {
        var results = await SendToExternalUsersAsync(
            [externalUserId],
            titleAr,
            titleEn,
            bodyAr,
            bodyEn,
            type,
            referenceId,
            data,
            targetUrl,
            profile,
            targetApplication,
            cancellationToken);

        return results.Count > 0
            ? results[0]
            : new OneSignalPushDispatchResult(false, false, true, null, null, "No push dispatch results were produced.");
    }

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

    Task<IReadOnlyList<OneSignalPushDispatchResult>> SendToExternalUsersAsync(
        IReadOnlyCollection<string> externalUserIds,
        string titleAr,
        string titleEn,
        string bodyAr,
        string bodyEn,
        string? type,
        Guid? referenceId,
        string? data,
        string? targetUrl,
        OneSignalPushProfile profile,
        OneSignalApplicationTarget targetApplication,
        CancellationToken cancellationToken = default);
}

public enum OneSignalPushProfile
{
    Default = 0,
    MobileHeadsUp = 1,
    MobileOrderUpdates = 2,
    MobileStandard = 3
}

public sealed record OneSignalPushDispatchResult(
    bool Attempted,
    bool Sent,
    bool Skipped,
    int? ProviderStatusCode,
    string? ProviderNotificationId,
    string? Reason);
