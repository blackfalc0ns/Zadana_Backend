using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Social.Enums;

namespace Zadana.Application.Modules.Delivery.Support;

public static class DriverExpiryLockNotificationDispatcher
{
    public static async Task NotifyAsync(
        Driver driver,
        INotificationService notificationService,
        IOneSignalPushService oneSignalPushService,
        CancellationToken cancellationToken)
    {
        var expiredDocuments = new List<string>();

        if (driver.NationalIdExpiryDate.HasValue && driver.NationalIdExpiryDate.Value.Date < DateTime.UtcNow.Date)
        {
            expiredDocuments.Add("NationalId");
        }

        if (driver.DriverLicenseExpiryDate.HasValue && driver.DriverLicenseExpiryDate.Value.Date < DateTime.UtcNow.Date)
        {
            expiredDocuments.Add("DriverLicense");
        }

        if (driver.VehicleLicenseExpiryDate.HasValue && driver.VehicleLicenseExpiryDate.Value.Date < DateTime.UtcNow.Date)
        {
            expiredDocuments.Add("VehicleLicense");
        }

        var data = DriverNotificationDataBuilder.Build(
            screen: "account_status",
            @event: "account.documents_expired_lock",
            driverId: driver.Id,
            extra: new
            {
                accountStatus = driver.Status.ToString(),
                verificationStatus = driver.VerificationStatus.ToString(),
                reason = "expired_documents",
                expiredDocuments
            });

        try
        {
            await notificationService.SendToUserAsync(
                driver.UserId,
                new NotificationDispatchRequest(
                    "تم إيقاف حسابك لانتهاء مستنداتك",
                    "Your account was locked due to expired documents",
                    "تم إيقاف حساب المندوب مؤقتًا لأن بعض مستندات الهوية أو الرخص منتهية. حدّث المستندات لإعادة التفعيل.",
                    "Your driver account was temporarily locked because one or more required documents expired. Renew the documents to reactivate your account.",
                    NotificationTypes.DriverAccountUpdated,
                    NotificationCategories.Account,
                    NotificationPriorities.Critical,
                    driver.Id,
                    data),
                cancellationToken);
        }
        catch
        {
        }

        try
        {
            await notificationService.SendDriverHomeUpdatedAsync(driver.UserId, cancellationToken);
        }
        catch
        {
        }

        try
        {
            await oneSignalPushService.SendMobileNotificationAsync(
                OneSignalMobilePushRequest.CreateHeadsUp(
                    driver.UserId.ToString(),
                    "تم إيقاف حسابك لانتهاء مستنداتك",
                    "Driver account locked due to expired documents",
                    "تم إيقاف حساب المندوب مؤقتًا لأن بعض المستندات المطلوبة انتهت صلاحيتها. يرجى تحديثها للعودة للعمل.",
                    "Your driver account was temporarily locked because required documents have expired. Please renew them to return to work.",
                    NotificationTypes.DriverAccountUpdated,
                    driver.Id,
                    data,
                    targetUrl: "/account-status",
                    category: NotificationCategories.Account,
                    targetApplication: OneSignalApplicationTarget.Driver),
                cancellationToken);
        }
        catch
        {
        }
    }
}
