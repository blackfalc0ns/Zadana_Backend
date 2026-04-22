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
        var data = JsonSerializer.Serialize(new
        {
            bannerId = notification.BannerId,
            imageUrl = notification.ImageUrl
        });

        var titleAr = $"🎉 عرض جديد: {notification.TitleAr}";
        var titleEn = $"🎉 New Offer: {notification.TitleEn}";
        const string bodyAr = "اكتشف أحدث العروض والخصومات المتاحة الآن!";
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
            targetUrl: null,
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
