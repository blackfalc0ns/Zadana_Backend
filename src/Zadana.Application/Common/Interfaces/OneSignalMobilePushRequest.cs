namespace Zadana.Application.Common.Interfaces;

public sealed record OneSignalMobilePushRequest(
    string ExternalUserId,
    string TitleAr,
    string TitleEn,
    string BodyAr,
    string BodyEn,
    string? Type,
    Guid? ReferenceId,
    string? Data,
    string? TargetUrl,
    string? Category,
    OneSignalPushProfile Profile)
{
    public static OneSignalMobilePushRequest CreateHeadsUp(
        string externalUserId,
        string titleAr,
        string titleEn,
        string bodyAr,
        string bodyEn,
        string? type = null,
        Guid? referenceId = null,
        string? data = null,
        string? targetUrl = null,
        string? category = null) =>
        new(
            externalUserId,
            titleAr,
            titleEn,
            bodyAr,
            bodyEn,
            type,
            referenceId,
            data,
            targetUrl,
            category,
            OneSignalPushProfile.MobileHeadsUp);

    public static OneSignalMobilePushRequest CreateStandard(
        string externalUserId,
        string titleAr,
        string titleEn,
        string bodyAr,
        string bodyEn,
        string? type = null,
        Guid? referenceId = null,
        string? data = null,
        string? targetUrl = null,
        string? category = null) =>
        new(
            externalUserId,
            titleAr,
            titleEn,
            bodyAr,
            bodyEn,
            type,
            referenceId,
            data,
            targetUrl,
            category,
            OneSignalPushProfile.MobileStandard);

    public Task<OneSignalPushDispatchResult> DispatchAsync(
        IOneSignalPushService oneSignalPushService,
        CancellationToken cancellationToken = default) =>
        string.IsNullOrWhiteSpace(Category)
            ? oneSignalPushService.SendToExternalUserAsync(
                ExternalUserId,
                TitleAr,
                TitleEn,
                BodyAr,
                BodyEn,
                Type,
                ReferenceId,
                Data,
                TargetUrl,
                Profile,
                cancellationToken)
            : oneSignalPushService.SendMobileNotificationAsync(this, cancellationToken);
}
