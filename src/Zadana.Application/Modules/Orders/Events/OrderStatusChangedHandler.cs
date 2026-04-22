using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Orders.Support;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
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
    private readonly IOrderStatusNotificationDispatcher _orderStatusNotificationDispatcher;

    public OrderStatusChangedHandler(
        INotificationService notificationService,
        IApplicationDbContext context,
        IOneSignalPushService oneSignalPushService,
        IOrderStatusNotificationDispatcher orderStatusNotificationDispatcher)
    {
        _notificationService = notificationService;
        _context = context;
        _oneSignalPushService = oneSignalPushService;
        _orderStatusNotificationDispatcher = orderStatusNotificationDispatcher;
    }

    public async Task Handle(OrderStatusChangedNotification notification, CancellationToken cancellationToken)
    {
        await HandleDirectPerOrderPayoutAsync(notification, cancellationToken);

        var targetUrl = OrderStatusNotificationComposer.ResolveTargetUrl(notification.OrderId);
        var action = OrderStatusNotificationComposer.ResolveAction(notification.NewStatus);
        var data = OrderStatusNotificationComposer.BuildData(
            notification.OrderId,
            notification.OrderNumber,
            notification.VendorId,
            notification.OldStatus,
            notification.NewStatus,
            notification.ActorRole,
            action,
            targetUrl);

        if (notification.NotifyCustomer && !notification.CustomerNotificationAlreadySent)
        {
            await _orderStatusNotificationDispatcher.DispatchCustomerAsync(
                new OrderStatusCustomerNotificationRequest(
                    notification.UserId,
                    notification.OrderId,
                    notification.VendorId,
                    notification.OrderNumber,
                    notification.OldStatus,
                    notification.NewStatus,
                    notification.ActorRole),
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

        await _notificationService.SendOrderStatusChangedToUserAsync(
            vendorRecipient.UserId,
            notification.OrderId,
            notification.OrderNumber,
            notification.VendorId,
            notification.OldStatus.ToString(),
            notification.NewStatus.ToString(),
            notification.ActorRole,
            action,
            targetUrl,
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
}
