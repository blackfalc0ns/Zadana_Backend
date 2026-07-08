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

        await CloseAssignmentsAndNotifyAsync(assignments, orderId, orderNumber, reason, cancellationToken);
    }

    /// <summary>
    /// Closes stale assignments that stayed open after their order reached a terminal status.
    /// This heals race conditions or legacy rows so drivers are not stuck "on mission".
    /// </summary>
    public async Task CloseAssignmentsLinkedToTerminalOrdersAsync(
        Guid? driverId = null,
        CancellationToken cancellationToken = default)
    {
        var assignments = await _context.DeliveryAssignments
            .Include(assignment => assignment.Driver)
            .Include(assignment => assignment.Order)
            .Where(assignment =>
                assignment.DriverId != null &&
                assignment.Status != AssignmentStatus.Delivered &&
                assignment.Status != AssignmentStatus.Failed &&
                assignment.Status != AssignmentStatus.Cancelled &&
                assignment.Status != AssignmentStatus.Returned &&
                assignment.Status != AssignmentStatus.Rejected &&
                (assignment.Order.Status == OrderStatus.Cancelled ||
                 assignment.Order.Status == OrderStatus.VendorRejected ||
                 assignment.Order.Status == OrderStatus.DeliveryFailed ||
                 assignment.Order.Status == OrderStatus.Refunded ||
                 assignment.Order.Status == OrderStatus.Delivered))
            .Where(assignment => !driverId.HasValue || assignment.DriverId == driverId)
            .ToListAsync(cancellationToken);

        if (assignments.Count == 0)
        {
            return;
        }

        await CloseAssignmentsAndNotifyAsync(
            assignments,
            orderId: null,
            orderNumber: null,
            reason: "Order is no longer active.",
            cancellationToken);
    }

    private async Task CloseAssignmentsAndNotifyAsync(
        IReadOnlyList<Domain.Modules.Delivery.Entities.DeliveryAssignment> assignments,
        Guid? orderId,
        string? orderNumber,
        string reason,
        CancellationToken cancellationToken)
    {
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

        if (orderId.HasValue)
        {
            _logger.LogInformation(
                "Closed {AssignmentCount} open delivery assignment(s) for order {OrderId} ({OrderNumber}).",
                assignments.Count,
                orderId.Value,
                orderNumber);
        }
        else
        {
            _logger.LogInformation(
                "Closed {AssignmentCount} stale delivery assignment(s) linked to terminal orders.",
                assignments.Count);
        }

        foreach (var (assignment, hadActiveOffer) in cancelledAssignments)
        {
            if (assignment.Driver?.UserId is not Guid driverUserId || driverUserId == Guid.Empty)
            {
                continue;
            }

            if (hadActiveOffer && orderId.HasValue && !string.IsNullOrWhiteSpace(orderNumber))
            {
                await NotifyOfferWithdrawnAsync(
                    driverUserId,
                    assignment.Id,
                    orderId.Value,
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
        var titleAr = "ألغينا عرض التوصيل";
        var titleEn = "Delivery offer cancelled";
        var bodyAr = $"ألغينا الطلب رقم #{orderNumber} ولم يعد عرض التوصيل متاحاً.";
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
