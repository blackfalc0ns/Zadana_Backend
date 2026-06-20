using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.Support;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Social.Enums;

namespace Zadana.Application.Modules.Delivery.Support;

public sealed class DeliveryAssignmentOrderCancellationService
{
    private readonly IApplicationDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly IOneSignalPushService _oneSignalPushService;
    private readonly ILogger<DeliveryAssignmentOrderCancellationService> _logger;

    public DeliveryAssignmentOrderCancellationService(
        IApplicationDbContext context,
        INotificationService notificationService,
        IOneSignalPushService oneSignalPushService,
        ILogger<DeliveryAssignmentOrderCancellationService> logger)
    {
        _context = context;
        _notificationService = notificationService;
        _oneSignalPushService = oneSignalPushService;
        _logger = logger;
    }

    public static bool ShouldCloseAssignments(OrderStatus newStatus) =>
        newStatus is OrderStatus.Cancelled
            or OrderStatus.VendorRejected
            or OrderStatus.DeliveryFailed
            or OrderStatus.Refunded;

    public async Task CloseOpenAssignmentsAsync(
        Guid orderId,
        string orderNumber,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var assignments = await _context.DeliveryAssignments
            .Include(assignment => assignment.Driver)
            .Where(assignment =>
                assignment.OrderId == orderId &&
                assignment.Status != AssignmentStatus.Delivered &&
                assignment.Status != AssignmentStatus.Failed &&
                assignment.Status != AssignmentStatus.Cancelled &&
                assignment.Status != AssignmentStatus.Returned &&
                assignment.Status != AssignmentStatus.Rejected)
            .ToListAsync(cancellationToken);

        if (assignments.Count == 0)
        {
            return;
        }

        var cancelledAssignments = new List<(Domain.Modules.Delivery.Entities.DeliveryAssignment Assignment, bool HadActiveOffer)>();

        foreach (var assignment in assignments)
        {
            var hadActiveOffer = assignment.Status == AssignmentStatus.OfferSent;
            assignment.Cancel(reason);
            cancelledAssignments.Add((assignment, hadActiveOffer));
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Closed {AssignmentCount} open delivery assignment(s) for order {OrderId} ({OrderNumber}).",
            assignments.Count,
            orderId,
            orderNumber);

        foreach (var (assignment, hadActiveOffer) in cancelledAssignments)
        {
            if (assignment.Driver?.UserId is not Guid driverUserId || driverUserId == Guid.Empty)
            {
                continue;
            }

            if (hadActiveOffer)
            {
                await NotifyOfferWithdrawnAsync(
                    driverUserId,
                    assignment.Id,
                    orderId,
                    orderNumber,
                    cancellationToken);
            }

            await _notificationService.SendDriverHomeUpdatedAsync(driverUserId, cancellationToken);
        }
    }

    private async Task NotifyOfferWithdrawnAsync(
        Guid driverUserId,
        Guid assignmentId,
        Guid orderId,
        string orderNumber,
        CancellationToken cancellationToken)
    {
        var titleAr = "تم إلغاء عرض التوصيل";
        var titleEn = "Delivery offer cancelled";
        var bodyAr = $"تم إلغاء الطلب رقم #{orderNumber} ولم يعد عرض التوصيل متاحاً.";
        var bodyEn = $"Order #{orderNumber} was cancelled and the delivery offer is no longer available.";

        var data = DriverNotificationDataBuilder.Build(
            screen: "home",
            @event: "dispatch.offer_cancelled",
            orderId: orderId,
            assignmentId: assignmentId,
            titleAr: titleAr,
            titleEn: titleEn,
            bodyAr: bodyAr,
            bodyEn: bodyEn,
            extra: new
            {
                orderNumber,
                source = "order_cancelled"
            });

        try
        {
            await _notificationService.SendToUserAsync(
                driverUserId,
                new NotificationDispatchRequest(
                    titleAr,
                    titleEn,
                    bodyAr,
                    bodyEn,
                    NotificationTypes.DriverDeliveryOffer,
                    NotificationCategories.Dispatch,
                    NotificationPriorities.High,
                    orderId,
                    data),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to persist offer-withdrawn inbox notification for driver user {DriverUserId} on order {OrderId}.",
                driverUserId,
                orderId);
        }

        try
        {
            await _oneSignalPushService.SendMobileNotificationAsync(
                OneSignalMobilePushRequest.CreateHeadsUp(
                    driverUserId.ToString(),
                    titleAr,
                    titleEn,
                    bodyAr,
                    bodyEn,
                    NotificationTypes.DriverDeliveryOffer,
                    orderId,
                    data,
                    targetUrl: "/",
                    category: NotificationCategories.Dispatch,
                    targetApplication: OneSignalApplicationTarget.Driver),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to send offer-withdrawn push for driver user {DriverUserId} on order {OrderId}.",
                driverUserId,
                orderId);
        }
    }
}
