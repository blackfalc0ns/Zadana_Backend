using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.Events;
using Zadana.Domain.Modules.Social.Enums;

namespace Zadana.Application.Modules.Catalog.EventHandlers;

/// <summary>
/// When a MasterProduct is deleted, notify all vendors who had active listings for it.
/// </summary>
public class MasterProductDeletedEventHandler : INotificationHandler<MasterProductDeletedEvent>
{
    private readonly IApplicationDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly ILogger<MasterProductDeletedEventHandler> _logger;

    public MasterProductDeletedEventHandler(
        IApplicationDbContext context,
        INotificationService notificationService,
        ILogger<MasterProductDeletedEventHandler> logger)
    {
        _context = context;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task Handle(MasterProductDeletedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            // Find vendors who had listings for this product (including soft-deleted ones)
            var affectedVendorUserIds = await _context.VendorProducts
                .IgnoreQueryFilters()
                .Where(vp => vp.MasterProductId == notification.ProductId)
                .Join(_context.Vendors,
                    vp => vp.VendorId,
                    v => v.Id,
                    (vp, v) => v.UserId)
                .Distinct()
                .ToListAsync(cancellationToken);

            foreach (var vendorUserId in affectedVendorUserIds)
            {
                var data = $"{{\"productId\":\"{notification.ProductId}\",\"nameAr\":\"{notification.NameAr}\",\"nameEn\":\"{notification.NameEn}\"}}";
                await _notificationService.SendToUserAsync(
                    vendorUserId,
                    "حذفنا منتج من الكاتالوج",
                    "Catalog Product Removed",
                    $"حذفنا المنتج \"{notification.NameAr}\" من كاتالوج المنتجات. قد يؤثر ذلك على منتجاتك.",
                    $"The product \"{notification.NameEn}\" has been removed from the catalog. This may affect your listings.",
                    NotificationTypes.VendorAccountUpdated,
                    notification.ProductId,
                    data,
                    cancellationToken);
            }

            _logger.LogInformation(
                "MasterProduct {ProductId} deletion notifications sent to {Count} vendors.",
                notification.ProductId, affectedVendorUserIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send deletion notifications for MasterProduct {ProductId}", notification.ProductId);
        }
    }
}
