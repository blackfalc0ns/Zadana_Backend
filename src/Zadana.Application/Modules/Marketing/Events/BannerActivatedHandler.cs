using System.Text.Json;
using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Social.Enums;

namespace Zadana.Application.Modules.Marketing.Events;

public class BannerActivatedHandler : INotificationHandler<BannerActivatedNotification>
{
    private readonly INotificationService _notificationService;

    public BannerActivatedHandler(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public async Task Handle(BannerActivatedNotification notification, CancellationToken cancellationToken)
    {
        var data = JsonSerializer.Serialize(new
        {
            bannerId = notification.BannerId,
            imageUrl = notification.ImageUrl
        });

        await _notificationService.BroadcastToAllCustomersAsync(
            titleAr: $"🎉 عرض جديد: {notification.TitleAr}",
            titleEn: $"🎉 New Offer: {notification.TitleEn}",
            bodyAr: "اكتشف أحدث العروض والخصومات المتاحة الآن!",
            bodyEn: "Discover the latest offers and discounts available now!",
            type: NotificationTypes.NewBanner,
            data: data,
            cancellationToken: cancellationToken);
    }
}
