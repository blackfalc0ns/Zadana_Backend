using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Social.Enums;

namespace Zadana.Application.Modules.Marketing.Events;

public class BannerActivatedHandler : INotificationHandler<BannerActivatedNotification>
{
    private readonly INotificationService _notificationService;
    private readonly IApplicationDbContext _context;
    private readonly IOneSignalPushService _oneSignalPushService;

    public BannerActivatedHandler(
        INotificationService notificationService,
        IApplicationDbContext context,
        IOneSignalPushService oneSignalPushService)
    {
        _notificationService = notificationService;
        _context = context;
        _oneSignalPushService = oneSignalPushService;
    }

    public async Task Handle(BannerActivatedNotification notification, CancellationToken cancellationToken)
    {
        const string targetUrl = "/";
        var data = JsonSerializer.Serialize(new
        {
            bannerId = notification.BannerId,
            imageUrl = notification.ImageUrl,
            category = "marketing",
            screen = "home",
            targetUrl,
            presentation = "popup",
            popupType = "new_banner",
            showPopup = true,
            eventName = "banner.activated"
        });

        var titleAr = $"\ud83c\udf89 \u0639\u0631\u0636 \u062c\u062f\u064a\u062f: {notification.TitleAr}";
        var titleEn = $"\ud83c\udf89 New Offer: {notification.TitleEn}";
        const string bodyAr = "\u0627\u0643\u062a\u0634\u0641 \u0623\u062d\u062f\u062b \u0627\u0644\u0639\u0631\u0648\u0636 \u0648\u0627\u0644\u062e\u0635\u0648\u0645\u0627\u062a \u0627\u0644\u0645\u062a\u0627\u062d\u0629 \u0627\u0644\u0622\u0646!";
        const string bodyEn = "Discover the latest offers and discounts available now!";

        await _notificationService.BroadcastToAllCustomersAsync(
            titleAr: titleAr,
            titleEn: titleEn,
            bodyAr: bodyAr,
            bodyEn: bodyEn,
            type: NotificationTypes.NewBanner,
            data: data,
            cancellationToken: cancellationToken);

        var externalUserIds = await GetTargetExternalUserIdsAsync(cancellationToken);
        if (externalUserIds.Count == 0)
        {
            return;
        }

        await _oneSignalPushService.SendToExternalUsersAsync(
            externalUserIds,
            titleAr,
            titleEn,
            bodyAr,
            bodyEn,
            type: NotificationTypes.NewBanner,
            data: data,
            targetUrl: targetUrl,
            profile: OneSignalPushProfile.MobileHeadsUp,
            cancellationToken: cancellationToken);
    }

    private async Task<List<string>> GetTargetExternalUserIdsAsync(CancellationToken cancellationToken)
    {
        var userIds = await (
            from device in _context.UserPushDevices.AsNoTracking()
            join user in _context.Users.AsNoTracking() on device.UserId equals user.Id
            where device.IsActive
                  && device.NotificationsEnabled
                  && user.Role == UserRole.Customer
            select device.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return userIds
            .Select(id => id.ToString())
            .ToList();
    }
}
