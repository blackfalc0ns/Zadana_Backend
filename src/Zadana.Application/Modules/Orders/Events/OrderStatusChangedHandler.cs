using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Social.Enums;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;

namespace Zadana.Application.Modules.Orders.Events;

public class OrderStatusChangedHandler : INotificationHandler<OrderStatusChangedNotification>
{
    private readonly INotificationService _notificationService;
    private readonly IApplicationDbContext _context;
    private readonly IOneSignalPushService _oneSignalPushService;

    public OrderStatusChangedHandler(
        INotificationService notificationService,
        IApplicationDbContext context,
        IOneSignalPushService oneSignalPushService)
    {
        _notificationService = notificationService;
        _context = context;
        _oneSignalPushService = oneSignalPushService;
    }

    public async Task Handle(OrderStatusChangedNotification notification, CancellationToken cancellationToken)
    {
        await HandleDirectPerOrderPayoutAsync(notification, cancellationToken);

        var targetUrl = ResolveTargetUrl(notification);
        var data = JsonSerializer.Serialize(new
        {
            orderId = notification.OrderId,
            orderNumber = notification.OrderNumber,
            vendorId = notification.VendorId,
            oldStatus = notification.OldStatus.ToString(),
            newStatus = notification.NewStatus.ToString(),
            actorRole = notification.ActorRole,
            action = ResolveAction(notification),
            targetUrl
        });

        if (notification.NotifyCustomer)
        {
            var (titleAr, titleEn, bodyAr, bodyEn) = GetCustomerNotificationContent(notification.NewStatus, notification.OrderNumber);

            await _notificationService.SendToUserAsync(
                notification.UserId,
                titleAr,
                titleEn,
                bodyAr,
                bodyEn,
                notification.NewStatus == OrderStatus.Cancelled ? NotificationTypes.OrderCancelled : NotificationTypes.OrderStatusChanged,
                notification.OrderId,
                data,
                cancellationToken);
        }

        if (!notification.NotifyVendor)
        {
            return;
        }

        var vendorRecipient = await _context.Vendors
            .AsNoTracking()
            .Where(vendor => vendor.Id == notification.VendorId)
            .Select(vendor => new
            {
                vendor.UserId,
                vendor.NewOrdersNotificationsEnabled
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (vendorRecipient is null)
        {
            return;
        }

        var (vendorTitleAr, vendorTitleEn, vendorBodyAr, vendorBodyEn, vendorType) =
            GetVendorNotificationContent(notification.NewStatus, notification.OrderNumber);

        await _notificationService.SendToUserAsync(
            vendorRecipient.UserId,
            vendorTitleAr,
            vendorTitleEn,
            vendorBodyAr,
            vendorBodyEn,
            vendorType,
            notification.OrderId,
            data,
            cancellationToken);

        if (notification.NewStatus == OrderStatus.PendingVendorAcceptance && !vendorRecipient.NewOrdersNotificationsEnabled)
        {
            return;
        }

        await _oneSignalPushService.SendToExternalUserAsync(
            vendorRecipient.UserId.ToString(),
            vendorTitleAr,
            vendorTitleEn,
            vendorBodyAr,
            vendorBodyEn,
            vendorType,
            notification.OrderId,
            data,
            targetUrl,
            cancellationToken);
    }

    private async Task HandleDirectPerOrderPayoutAsync(
        OrderStatusChangedNotification notification,
        CancellationToken cancellationToken)
    {
        if (notification.NewStatus != OrderStatus.Delivered)
        {
            return;
        }

        var vendor = await _context.Vendors
            .AsNoTracking()
            .Where(item => item.Id == notification.VendorId)
            .Select(item => new
            {
                item.Id,
                item.FinancialLifecycleMode
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (vendor is null || vendor.FinancialLifecycleMode != VendorFinancialLifecycleMode.PerOrderDirectPayout)
        {
            return;
        }

        var alreadySettled = await _context.SettlementItems
            .AsNoTracking()
            .AnyAsync(item => item.OrderId == notification.OrderId, cancellationToken);

        if (alreadySettled)
        {
            return;
        }

        var order = await _context.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == notification.OrderId, cancellationToken);

        if (order is null)
        {
            return;
        }

        var primaryBankAccount = await _context.VendorBankAccounts
            .AsNoTracking()
            .Where(item => item.VendorId == notification.VendorId)
            .OrderByDescending(item => item.IsPrimary)
            .ThenByDescending(item => item.VerifiedAtUtc)
            .ThenByDescending(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (primaryBankAccount is null)
        {
            return;
        }

        var settlement = new Settlement(notification.VendorId, null, SettlementOrigin.DirectPerOrder);
        settlement.UpdateTotals(order.TotalAmount, order.CommissionAmount);
        _context.Settlements.Add(settlement);
        await _context.SaveChangesAsync(cancellationToken);

        _context.SettlementItems.Add(new SettlementItem(
            settlement.Id,
            order.Id,
            settlement.NetAmount,
            0m,
            settlement.CommissionAmount,
            order.PaymentMethod == PaymentMethodType.CashOnDelivery ? order.TotalAmount : 0m));

        _context.Payouts.Add(new Payout(settlement.Id, settlement.NetAmount, primaryBankAccount.Id));
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static (string TitleAr, string TitleEn, string BodyAr, string BodyEn) GetCustomerNotificationContent(
        OrderStatus status, string orderNumber)
    {
        return status switch
        {
            OrderStatus.Placed => (
                "تم تأكيد الطلب",
                "Order Confirmed",
                $"تم تأكيد طلبك رقم {orderNumber} بنجاح",
                $"Your order #{orderNumber} has been confirmed successfully"),

            OrderStatus.PendingVendorAcceptance => (
                "في انتظار موافقة التاجر",
                "Awaiting Vendor Approval",
                $"طلبك رقم {orderNumber} في انتظار موافقة التاجر",
                $"Your order #{orderNumber} is awaiting vendor approval"),

            OrderStatus.Accepted => (
                "تم قبول الطلب",
                "Order Accepted",
                $"تم قبول طلبك رقم {orderNumber} من قبل التاجر",
                $"Your order #{orderNumber} has been accepted by the vendor"),

            OrderStatus.VendorRejected => (
                "تم رفض الطلب",
                "Order Rejected",
                $"للأسف، تم رفض طلبك رقم {orderNumber} من قبل التاجر",
                $"Sorry, your order #{orderNumber} has been rejected by the vendor"),

            OrderStatus.Preparing => (
                "جاري تحضير الطلب",
                "Order Being Prepared",
                $"طلبك رقم {orderNumber} قيد التحضير الآن",
                $"Your order #{orderNumber} is now being prepared"),

            OrderStatus.ReadyForPickup => (
                "الطلب جاهز للاستلام",
                "Order Ready for Pickup",
                $"طلبك رقم {orderNumber} جاهز وفي انتظار المندوب",
                $"Your order #{orderNumber} is ready and waiting for the driver"),

            OrderStatus.DriverAssigned => (
                "تم تعيين مندوب التوصيل",
                "Driver Assigned",
                $"تم تعيين مندوب لتوصيل طلبك رقم {orderNumber}",
                $"A driver has been assigned to deliver your order #{orderNumber}"),

            OrderStatus.PickedUp => (
                "تم استلام الطلب من التاجر",
                "Order Picked Up",
                $"المندوب استلم طلبك رقم {orderNumber} من التاجر",
                $"The driver has picked up your order #{orderNumber} from the vendor"),

            OrderStatus.OnTheWay => (
                "الطلب في الطريق إليك",
                "Order On The Way",
                $"طلبك رقم {orderNumber} في الطريق إليك الآن!",
                $"Your order #{orderNumber} is on its way to you!"),

            OrderStatus.Delivered => (
                "تم التوصيل بنجاح",
                "Order Delivered",
                $"تم توصيل طلبك رقم {orderNumber} بنجاح. شكراً لك!",
                $"Your order #{orderNumber} has been delivered successfully. Thank you!"),

            OrderStatus.DeliveryFailed => (
                "فشل التوصيل",
                "Delivery Failed",
                $"للأسف، فشل توصيل طلبك رقم {orderNumber}. سيتم التواصل معك",
                $"Sorry, delivery of your order #{orderNumber} failed. We will contact you"),

            OrderStatus.Cancelled => (
                "تم إلغاء الطلب",
                "Order Cancelled",
                $"تم إلغاء طلبك رقم {orderNumber}",
                $"Your order #{orderNumber} has been cancelled"),

            OrderStatus.Refunded => (
                "تم استرداد المبلغ",
                "Order Refunded",
                $"تم استرداد مبلغ طلبك رقم {orderNumber}",
                $"Your order #{orderNumber} has been refunded"),

            _ => (
                "تحديث على الطلب",
                "Order Update",
                $"تم تحديث حالة طلبك رقم {orderNumber}",
                $"Your order #{orderNumber} status has been updated")
        };
    }

    private static (string TitleAr, string TitleEn, string BodyAr, string BodyEn, string Type) GetVendorNotificationContent(
        OrderStatus status,
        string orderNumber)
    {
        return status switch
        {
            OrderStatus.PendingVendorAcceptance => (
                "طلب جديد بانتظار التأكيد",
                "New order awaiting confirmation",
                $"يوجد طلب جديد رقم {orderNumber} بانتظار موافقتك.",
                $"Order #{orderNumber} is waiting for your confirmation.",
                NotificationTypes.VendorNewOrder),
            OrderStatus.Cancelled => (
                "تم إلغاء الطلب",
                "Order cancelled",
                $"قام العميل بإلغاء الطلب رقم {orderNumber}.",
                $"The customer cancelled order #{orderNumber}.",
                NotificationTypes.OrderCancelled),
            OrderStatus.DeliveryFailed => (
                "تعذر تسليم الطلب",
                "Delivery failed",
                $"تعذر تسليم الطلب رقم {orderNumber} ويحتاج إلى متابعة.",
                $"Delivery failed for order #{orderNumber} and needs follow-up.",
                NotificationTypes.OrderStatusChanged),
            _ => (
                "تحديث على الطلب",
                "Order update",
                $"تم تحديث الطلب رقم {orderNumber}.",
                $"Order #{orderNumber} has been updated.",
                NotificationTypes.OrderStatusChanged)
        };
    }

    private static string ResolveAction(OrderStatusChangedNotification notification) =>
        notification.NewStatus switch
        {
            OrderStatus.PendingVendorAcceptance => "placed",
            OrderStatus.Cancelled => "cancelled",
            _ => "status_changed"
        };

    private static string ResolveTargetUrl(OrderStatusChangedNotification notification) =>
        $"/orders/{notification.OrderId}";
}
