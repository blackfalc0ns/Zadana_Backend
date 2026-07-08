using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Social.Enums;
using Zadana.SharedKernel.Serialization;

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

        if (driver.NationalIdExpiryDate.HasValue && driver.NationalIdExpiryDate.Value.Date < SaudiTime.Today)
        {
            expiredDocuments.Add("NationalId");
        }

        if (driver.DriverLicenseExpiryDate.HasValue && driver.DriverLicenseExpiryDate.Value.Date < SaudiTime.Today)
        {
            expiredDocuments.Add("DriverLicense");
        }

        if (driver.VehicleLicenseExpiryDate.HasValue && driver.VehicleLicenseExpiryDate.Value.Date < SaudiTime.Today)
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

        const string titleAr = "أوقفنا حسابك لانتهاء مستنداتك";
        const string titleEn = "Your account was locked due to expired documents";
        const string bodyAr = "أوقفنا حساب المندوب مؤقتا لأن بعض مستندات الهوية أو الرخص منتهية. حدث المستندات لإعادة التفعيل.";
        const string bodyEn = "Your driver account was temporarily locked because one or more required documents expired. Renew the documents to reactivate your account.";

        try
        {
            await notificationService.SendToUserAsync(
                driver.UserId,
                new NotificationDispatchRequest(
                    titleAr,
                    titleEn,
                    bodyAr,
                    bodyEn,
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
                    titleAr,
                    titleEn,
                    bodyAr,
                    bodyEn,
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
