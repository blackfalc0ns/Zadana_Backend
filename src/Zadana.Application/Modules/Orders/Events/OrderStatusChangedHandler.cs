using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.EmailCenter;
using Zadana.Application.Modules.EmailCenter.DTOs;
using Zadana.Application.Modules.EmailCenter.Interfaces;
using Zadana.Application.Modules.Delivery.Support;
using Zadana.Application.Modules.Orders.Support;
using Zadana.Application.Modules.Wallets.Services;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Identity.Enums;
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
    private readonly OrderRevenueDistributionService _revenueDistributionService;
    private readonly IOrderTrackingRealtimeNotifier _orderTrackingRealtimeNotifier;
    private readonly IEmailCenterService _emailCenterService;
    private readonly DeliveryAssignmentOrderCancellationService _deliveryAssignmentOrderCancellationService;
    private readonly ILogger<OrderStatusChangedHandler> _logger;

    public OrderStatusChangedHandler(
        INotificationService notificationService,
        IApplicationDbContext context,
        IOneSignalPushService oneSignalPushService,
        IOrderStatusNotificationDispatcher orderStatusNotificationDispatcher,
        OrderRevenueDistributionService revenueDistributionService,
        IOrderTrackingRealtimeNotifier orderTrackingRealtimeNotifier,
        IEmailCenterService emailCenterService,
        DeliveryAssignmentOrderCancellationService deliveryAssignmentOrderCancellationService,
        ILogger<OrderStatusChangedHandler> logger)
    {
        _notificationService = notificationService;
        _context = context;
        _oneSignalPushService = oneSignalPushService;
        _orderStatusNotificationDispatcher = orderStatusNotificationDispatcher;
        _revenueDistributionService = revenueDistributionService;
        _orderTrackingRealtimeNotifier = orderTrackingRealtimeNotifier;
        _emailCenterService = emailCenterService;
        _deliveryAssignmentOrderCancellationService = deliveryAssignmentOrderCancellationService;
        _logger = logger;
    }

    public async Task Handle(OrderStatusChangedNotification notification, CancellationToken cancellationToken)
    {
        await _revenueDistributionService.DistributeAsync(notification.OrderId, cancellationToken);

        // Broadcast the status change to everyone tracking the order in real time
        // (customer, vendor, driver, admin). Failures here are logged inside the notifier
        // and never break the rest of the dispatch pipeline.
        await _orderTrackingRealtimeNotifier.BroadcastOrderStatusChangedAsync(
            notification.OrderId,
            notification.OrderNumber,
            notification.VendorId,
            notification.UserId,
            notification.OldStatus,
            notification.NewStatus,
            notification.ActorRole,
            cancellationToken);

        if (DeliveryAssignmentOrderCancellationService.ShouldCloseAssignments(notification.NewStatus))
        {
            var cancellationReason = notification.NewStatus switch
            {
                OrderStatus.VendorRejected => "Order rejected by vendor.",
                OrderStatus.DeliveryFailed => "Order delivery failed.",
                OrderStatus.Refunded => "Order refunded.",
                _ => "Order cancelled."
            };

            await _deliveryAssignmentOrderCancellationService.CloseOpenAssignmentsAsync(
                notification.OrderId,
                notification.OrderNumber,
                cancellationReason,
                cancellationToken);

            await _deliveryAssignmentOrderCancellationService.CloseAssignmentsLinkedToTerminalOrdersAsync(
                cancellationToken: cancellationToken);
        }

        var targetUrl = OrderStatusNotificationComposer.ResolveTargetUrl(notification.OrderId);
        var action = OrderStatusNotificationComposer.ResolveAction(notification.NewStatus);
        var orderContext = await _context.Orders
            .AsNoTracking()
            .Where(order => order.Id == notification.OrderId)
            .Select(order => new { order.VendorBranchId, order.Fulfillment })
            .FirstOrDefaultAsync(cancellationToken);
        var data = AddBranchIdToData(
            OrderStatusNotificationComposer.BuildData(
                notification.OrderId,
                notification.OrderNumber,
                notification.VendorId,
                notification.OldStatus,
                notification.NewStatus,
                notification.ActorRole,
                action,
                targetUrl,
                orderContext?.Fulfillment ?? FulfillmentType.Delivery),
            orderContext?.VendorBranchId);

        if (notification.NotifyCustomer)
        {
            // Idempotent OTP / retry publishes (same status) must not re-push "تم التسليم".
            var statusActuallyChanged = notification.OldStatus != notification.NewStatus;

            if (statusActuallyChanged && !notification.CustomerNotificationAlreadySent)
            {
                await _orderStatusNotificationDispatcher.DispatchCustomerAsync(
                    new OrderStatusCustomerNotificationRequest(
                        notification.UserId,
                        notification.OrderId,
                        notification.VendorId,
                        notification.OrderNumber,
                        notification.OldStatus,
                        notification.NewStatus,
                        notification.ActorRole,
                        orderContext?.Fulfillment ?? FulfillmentType.Delivery),
                    cancellationToken);
            }

            if (statusActuallyChanged)
            {
                await DispatchCustomerOrderEmailAsync(notification, cancellationToken);
            }
        }

        await SendRealtimeToAssignedDriverAsync(notification, action, targetUrl, cancellationToken);

        if (!notification.NotifyVendor)
        {
            return;
        }

        if (!await IsVendorNotificationAllowedAsync(notification.OrderId, cancellationToken))
        {
            _logger.LogWarning(
                "Skipping vendor notification for order {OrderId}: card payment is not confirmed for vendor fulfillment.",
                notification.OrderId);
            return;
        }

        var vendorRecipient = await _context.Vendors
            .AsNoTracking()
            .Where(vendor => vendor.Id == notification.VendorId)
            .Select(vendor => new
            {
                vendor.UserId,
                vendor.NewOrdersNotificationsEnabled,
                vendor.EmailNotificationsEnabled,
                vendor.OwnerEmail,
                vendor.ContactEmail,
                vendor.BusinessNameAr,
                vendor.BusinessNameEn
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (vendorRecipient is null)
        {
            return;
        }

        // Branch-bound orders (pickup / resolved delivery branch) must NOT fan out to the
        // vendor owner / main branch — only staff scoped to that exact branch.
        var isBranchBoundOrder = orderContext?.VendorBranchId.HasValue == true;

        if (!isBranchBoundOrder)
        {
            await DispatchVendorOrderActionEmailAsync(
                notification,
                vendorRecipient.EmailNotificationsEnabled,
                vendorRecipient.OwnerEmail,
                vendorRecipient.ContactEmail,
                vendorRecipient.BusinessNameAr,
                vendorRecipient.BusinessNameEn,
                cancellationToken);
        }

        var (vendorTitleAr, vendorTitleEn, vendorBodyAr, vendorBodyEn, vendorType) =
            GetVendorNotificationContent(notification.NewStatus, notification.OrderNumber);

        var recipientUserIds = await GetVendorNotificationRecipientUserIdsAsync(
            notification.VendorId,
            orderContext?.VendorBranchId,
            vendorRecipient.UserId,
            cancellationToken);

        if (recipientUserIds.Count == 0)
        {
            _logger.LogWarning(
                "No vendor notification recipients for order {OrderId} vendor {VendorId} branch {BranchId}.",
                notification.OrderId,
                notification.VendorId,
                orderContext?.VendorBranchId);
            return;
        }

        foreach (var recipientUserId in recipientUserIds)
        {
            await _notificationService.SendToUserAsync(
                recipientUserId,
                vendorTitleAr,
                vendorTitleEn,
                vendorBodyAr,
                vendorBodyEn,
                vendorType,
                notification.OrderId,
                data,
                cancellationToken);

            await _notificationService.SendOrderStatusChangedToUserAsync(
                recipientUserId,
                notification.OrderId,
                notification.OrderNumber,
                notification.VendorId,
                notification.OldStatus.ToString(),
                notification.NewStatus.ToString(),
                notification.ActorRole,
                action,
                targetUrl,
                cancellationToken,
                showPopup: true);
        }

        if (notification.NewStatus == OrderStatus.PendingVendorAcceptance && !vendorRecipient.NewOrdersNotificationsEnabled)
        {
            return;
        }

        foreach (var recipientUserId in recipientUserIds)
        {
            var pushResult = await _oneSignalPushService.SendToExternalUserAsync(
                recipientUserId.ToString(),
                vendorTitleAr,
                vendorTitleEn,
                vendorBodyAr,
                vendorBodyEn,
                vendorType,
                notification.OrderId,
                data,
                targetUrl,
                OneSignalPushProfile.Default,
                OneSignalApplicationTarget.VendorWeb,
                cancellationToken);

            if (pushResult.Sent)
            {
                _logger.LogInformation(
                    "Vendor OneSignal push delivered for order {OrderId} user {UserId}. Type: {Type}. ProviderNotificationId: {ProviderNotificationId}.",
                    notification.OrderId,
                    recipientUserId,
                    vendorType,
                    pushResult.ProviderNotificationId);
                continue;
            }

            if (pushResult.Skipped)
            {
                _logger.LogWarning(
                    "Vendor OneSignal push skipped for order {OrderId} user {UserId}. Type: {Type}. Reason: {Reason}",
                    notification.OrderId,
                    recipientUserId,
                    vendorType,
                    pushResult.Reason);
                continue;
            }

            _logger.LogWarning(
                "Vendor OneSignal push failed for order {OrderId} user {UserId}. Type: {Type}. ProviderStatusCode: {ProviderStatusCode}. Reason: {Reason}",
                notification.OrderId,
                recipientUserId,
                vendorType,
                pushResult.ProviderStatusCode,
                pushResult.Reason);
        }
    }

    private async Task<List<Guid>> GetVendorNotificationRecipientUserIdsAsync(
        Guid vendorId,
        Guid? branchId,
        Guid vendorOwnerUserId,
        CancellationToken cancellationToken)
    {
        // Branch-bound: only that branch's staff. Owner is included ONLY when the branch is
        // the vendor primary branch (main store account), never for secondary branches.
        if (branchId.HasValue)
        {
            var branchStaffUserIds = await (
                from scope in _context.UserAccessScopes.AsNoTracking()
                join branch in _context.VendorBranches.AsNoTracking()
                    on scope.ScopeEntityId equals branch.Id
                where scope.IsActive &&
                      scope.PanelScope == PanelScope.VendorPanel &&
                      scope.ScopeType == AccessScopeType.VendorBranch &&
                      scope.ScopeEntityId == branchId.Value &&
                      branch.VendorId == vendorId
                select scope.UserId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var isPrimaryBranch = await _context.VendorBranches
                .AsNoTracking()
                .AnyAsync(
                    branch => branch.Id == branchId.Value &&
                              branch.VendorId == vendorId &&
                              branch.IsPrimary,
                    cancellationToken);

            if (isPrimaryBranch)
            {
                branchStaffUserIds.Add(vendorOwnerUserId);
            }

            return branchStaffUserIds.Distinct().ToList();
        }

        // Company-wide orders (no branch): owner + VendorCompany staff.
        var companyUserIds = await (
            from scope in _context.UserAccessScopes.AsNoTracking()
            where scope.IsActive &&
                  scope.PanelScope == PanelScope.VendorPanel &&
                  scope.ScopeType == AccessScopeType.VendorCompany &&
                  scope.ScopeEntityId == vendorId
            select scope.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return companyUserIds
            .Append(vendorOwnerUserId)
            .Distinct()
            .ToList();
    }

    private static string AddBranchIdToData(string data, Guid? branchId)
    {
        if (!branchId.HasValue)
        {
            return data;
        }

        try
        {
            var payload = JsonSerializer.Deserialize<Dictionary<string, object?>>(data) ?? [];
            payload["branchId"] = branchId.Value;
            return JsonSerializer.Serialize(payload);
        }
        catch
        {
            return data;
        }
    }

    private async Task DispatchCustomerOrderEmailAsync(
        OrderStatusChangedNotification notification,
        CancellationToken cancellationToken)
    {
        var eventKey = ResolveCustomerEmailEventKey(notification.NewStatus);
        if (eventKey is null)
        {
            return;
        }

        try
        {
            var order = await _context.Orders
                .AsNoTracking()
                .Where(item => item.Id == notification.OrderId)
                .Select(item => new
                {
                    item.OrderNumber,
                    item.TotalAmount,
                    item.Currency,
                    item.UserId,
                    item.PaymentMethod,
                    item.PaymentStatus,
                    CustomerName = item.User.FullName,
                    CustomerEmail = item.User.Email,
                    VendorName = string.IsNullOrWhiteSpace(item.Vendor.BusinessNameEn)
                        ? item.Vendor.BusinessNameAr
                        : item.Vendor.BusinessNameEn
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (order is null)
            {
                return;
            }

            if (eventKey == EmailEventKeys.CustomerOrderConfirmed &&
                !IsCustomerConfirmationEmailAllowed(order.PaymentMethod, order.PaymentStatus))
            {
                return;
            }

            await _emailCenterService.DispatchSystemEventEmailAsync(
                new EmailSystemEventDispatchRequest(
                    EventKey: eventKey,
                    AudienceType: "customers",
                    To: NormalizeRecipient(order.CustomerEmail),
                    Variables: new Dictionary<string, string>
                    {
                        ["customer_name"] = string.IsNullOrWhiteSpace(order.CustomerName) ? "Customer" : order.CustomerName,
                        ["order_number"] = order.OrderNumber,
                        ["vendor_name"] = order.VendorName,
                        ["order_total"] = FormatMoney(order.TotalAmount),
                        ["currency"] = string.IsNullOrWhiteSpace(order.Currency) ? "SAR" : order.Currency,
                        ["update_message"] = BuildCustomerImportantUpdateMessage(notification.NewStatus, order.OrderNumber)
                    },
                    TargetUrl: OrderStatusNotificationComposer.ResolveTargetUrl(notification.OrderId),
                    EntityId: notification.OrderId,
                    RecipientEntityId: order.UserId,
                    VendorId: notification.VendorId),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Customer order email dispatch failed for order {OrderId}", notification.OrderId);
        }
    }

    private async Task DispatchVendorOrderActionEmailAsync(
        OrderStatusChangedNotification notification,
        bool emailNotificationsEnabled,
        string? ownerEmail,
        string? contactEmail,
        string businessNameAr,
        string businessNameEn,
        CancellationToken cancellationToken)
    {
        if (notification.NewStatus != OrderStatus.PendingVendorAcceptance || !emailNotificationsEnabled)
        {
            return;
        }

        try
        {
            var order = await _context.Orders
                .AsNoTracking()
                .Where(item => item.Id == notification.OrderId)
                .Select(item => new
                {
                    item.OrderNumber,
                    item.TotalAmount,
                    item.Currency,
                    item.VendorBranchId
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (order is null)
            {
                return;
            }

            var vendorName = string.IsNullOrWhiteSpace(businessNameEn) ? businessNameAr : businessNameEn;
            await _emailCenterService.DispatchSystemEventEmailAsync(
                new EmailSystemEventDispatchRequest(
                    EventKey: EmailEventKeys.VendorOrderActionRequired,
                    AudienceType: "vendor_network",
                    To: NormalizeRecipient(ResolveFirstEmail(ownerEmail, contactEmail)),
                    Variables: new Dictionary<string, string>
                    {
                        ["vendor_name"] = vendorName,
                        ["order_number"] = order.OrderNumber,
                        ["order_total"] = FormatMoney(order.TotalAmount),
                        ["currency"] = string.IsNullOrWhiteSpace(order.Currency) ? "SAR" : order.Currency
                    },
                    TargetUrl: OrderStatusNotificationComposer.ResolveTargetUrl(notification.OrderId),
                    EntityId: notification.OrderId,
                    VendorId: notification.VendorId,
                    BranchId: order.VendorBranchId),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Vendor order-action email dispatch failed for order {OrderId}", notification.OrderId);
        }
    }

    private async Task<bool> IsVendorNotificationAllowedAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .AsNoTracking()
            .Where(item => item.Id == orderId)
            .Select(item => new
            {
                item.PaymentMethod,
                item.PaymentStatus
            })
            .FirstOrDefaultAsync(cancellationToken);

        return order is null ||
               order.PaymentMethod is not (PaymentMethodType.Card or PaymentMethodType.ApplePay or PaymentMethodType.Mada or PaymentMethodType.BankTransfer) ||
               order.PaymentStatus == PaymentStatus.Paid ||
               order.PaymentStatus == PaymentStatus.Refunded ||
               order.PaymentStatus == PaymentStatus.PartiallyRefunded;
    }

    private static string? ResolveCustomerEmailEventKey(OrderStatus newStatus) =>
        newStatus switch
        {
            OrderStatus.Placed or OrderStatus.PendingVendorAcceptance => EmailEventKeys.CustomerOrderConfirmed,
            OrderStatus.Cancelled or OrderStatus.VendorRejected or OrderStatus.DeliveryFailed or OrderStatus.Refunded => EmailEventKeys.CustomerOrderImportantUpdate,
            _ => null
        };

    private static bool IsCustomerConfirmationEmailAllowed(PaymentMethodType paymentMethod, PaymentStatus paymentStatus) =>
        paymentMethod is not (PaymentMethodType.Card or PaymentMethodType.ApplePay or PaymentMethodType.Mada or PaymentMethodType.BankTransfer) || paymentStatus == PaymentStatus.Paid;

    private static string BuildCustomerImportantUpdateMessage(OrderStatus status, string orderNumber) =>
        status switch
        {
            OrderStatus.Cancelled => $"Order {orderNumber} was cancelled. Open the app for details.",
            OrderStatus.VendorRejected => $"Order {orderNumber} was not accepted by the vendor. Open the app for details.",
            OrderStatus.DeliveryFailed => $"Delivery failed for order {orderNumber}. Our team will follow up if needed.",
            OrderStatus.Refunded => $"Order {orderNumber} has been refunded.",
            _ => $"There is an important update for order {orderNumber}."
        };

    private static string? ResolveFirstEmail(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static IReadOnlyList<string> NormalizeRecipient(string? email) =>
        string.IsNullOrWhiteSpace(email) ? [] : [email.Trim()];

    private static string FormatMoney(decimal value) => value.ToString("0.##");

    private async Task SendRealtimeToAssignedDriverAsync(
        OrderStatusChangedNotification notification,
        string action,
        string targetUrl,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[DriverRealtime] Looking for active assignment for order {OrderId} (status change: {OldStatus} -> {NewStatus}, actor: {Actor})",
            notification.OrderId, notification.OldStatus, notification.NewStatus, notification.ActorRole);

        var includeTerminalAssignments = ShouldIncludeTerminalAssignmentsForDriver(notification.NewStatus);

        var driverAssignment = await _context.DeliveryAssignments
            .AsNoTracking()
            .Where(assignment =>
                assignment.OrderId == notification.OrderId &&
                assignment.DriverId != null &&
                assignment.Status != AssignmentStatus.SearchingDriver &&
                assignment.Status != AssignmentStatus.OfferSent &&
                assignment.Status != AssignmentStatus.Rejected &&
                (includeTerminalAssignments ||
                    (assignment.Status != AssignmentStatus.Cancelled &&
                     assignment.Status != AssignmentStatus.Failed)))
            .OrderByDescending(assignment => assignment.CreatedAtUtc)
            .Select(assignment => new { assignment.Id, assignment.Status, DriverUserId = assignment.Driver!.UserId })
            .FirstOrDefaultAsync(cancellationToken);

        if (driverAssignment is null)
        {
            _logger.LogWarning(
                "[DriverRealtime] NO active assignment found for order {OrderId}. ReceiveAssignmentUpdated will NOT be sent.",
                notification.OrderId);
            return;
        }

        _logger.LogInformation(
            "[DriverRealtime] Found assignment {AssignmentId} (status={AssignmentStatus}) for driver user {DriverUserId}. Sending ReceiveOrderStatusChanged + ReceiveAssignmentUpdated.",
            driverAssignment.Id, driverAssignment.Status, driverAssignment.DriverUserId);

        await SendDriverAssignmentInboxAndPushAsync(
            notification,
            driverAssignment.Id,
            driverAssignment.DriverUserId,
            cancellationToken);

        await _notificationService.SendOrderStatusChangedToUserAsync(
            driverAssignment.DriverUserId,
            notification.OrderId,
            notification.OrderNumber,
            notification.VendorId,
            notification.OldStatus.ToString(),
            notification.NewStatus.ToString(),
            notification.ActorRole,
            action,
            targetUrl,
            cancellationToken,
            showPopup: true);

        // Push full assignment detail so the driver's order detail page refreshes in real-time
        await _notificationService.SendAssignmentUpdatedToDriverAsync(
            driverAssignment.DriverUserId,
            driverAssignment.Id,
            notification.OrderId,
            cancellationToken);

        await _notificationService.SendDriverHomeUpdatedAsync(
            driverAssignment.DriverUserId,
            cancellationToken);

        _logger.LogInformation(
            "[DriverRealtime] Successfully dispatched ReceiveAssignmentUpdated to driver user {DriverUserId} for assignment {AssignmentId}.",
            driverAssignment.DriverUserId, driverAssignment.Id);
    }

    private static bool ShouldIncludeTerminalAssignmentsForDriver(OrderStatus newStatus) =>
        newStatus is OrderStatus.Cancelled or OrderStatus.DeliveryFailed or OrderStatus.Refunded;

    private async Task SendDriverAssignmentInboxAndPushAsync(
        OrderStatusChangedNotification notification,
        Guid assignmentId,
        Guid driverUserId,
        CancellationToken cancellationToken)
    {
        if (string.Equals(notification.ActorRole, "driver", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug(
                "Skipping driver assignment inbox/push for order {OrderId} because actor is driver (status {NewStatus}).",
                notification.OrderId,
                notification.NewStatus);
            return;
        }

        var envelope = TryComposeDriverAssignmentNotification(notification, assignmentId, driverUserId);
        if (envelope is null)
        {
            _logger.LogDebug(
                "No driver assignment push composed for order {OrderId} status {NewStatus}.",
                notification.OrderId,
                notification.NewStatus);
            return;
        }

        await _notificationService.SendToUserAsync(
            driverUserId,
            envelope.Request,
            cancellationToken);

        // Use direct dispatch (subscription-first) so assignment pushes reach killed/background driver apps,
        // matching the delivery-offer path in DeliveryDispatchService.
        var pushResult = await _oneSignalPushService.SendMobileNotificationDirectAsync(
            envelope.PushRequest,
            cancellationToken);

        if (pushResult.Sent)
        {
            _logger.LogInformation(
                "Driver assignment push delivered for order {OrderId} driver user {DriverUserId}. Event: {EventName}. ProviderNotificationId: {ProviderNotificationId}.",
                notification.OrderId,
                driverUserId,
                ExtractAssignmentEventName(envelope.PushRequest.Data),
                pushResult.ProviderNotificationId);
            return;
        }

        if (pushResult.Skipped)
        {
            _logger.LogWarning(
                "Driver assignment push skipped for order {OrderId} driver user {DriverUserId}. Event: {EventName}. Reason: {Reason}",
                notification.OrderId,
                driverUserId,
                ExtractAssignmentEventName(envelope.PushRequest.Data),
                pushResult.Reason);
            return;
        }

        _logger.LogWarning(
            "Driver assignment push failed for order {OrderId} driver user {DriverUserId}. Event: {EventName}. ProviderStatusCode: {ProviderStatusCode}. Reason: {Reason}",
            notification.OrderId,
            driverUserId,
            ExtractAssignmentEventName(envelope.PushRequest.Data),
            pushResult.ProviderStatusCode,
            pushResult.Reason);
    }

    private static DriverAssignmentNotificationEnvelope? TryComposeDriverAssignmentNotification(
        OrderStatusChangedNotification notification,
        Guid assignmentId,
        Guid driverUserId)
    {
        var screen = "assignment_detail";

        return notification.NewStatus switch
            {
                OrderStatus.DriverAssigned => CreateDriverAssignmentNotification(
                    notification,
                    assignmentId,
                    driverUserId,
                    screen,
                    "assignment.driver_assigned",
                "عيّنا طلب جديد لك",
                "A delivery was assigned to you",
                $"عيّنا الطلب رقم #{notification.OrderNumber} لك. افتح تفاصيل المهمة لبدء التنفيذ.",
                $"Order #{notification.OrderNumber} was assigned to you. Open the assignment to get started.",
                NotificationPriorities.High,
                OneSignalPushRequestKind.HeadsUp),

                OrderStatus.Preparing => CreateDriverAssignmentNotification(
                    notification,
                    assignmentId,
                    driverUserId,
                    screen,
                    "assignment.vendor_preparing",
                "التاجر بدأ تحضير الطلب",
                "Vendor is preparing the order",
                $"التاجر بدأ تحضير الطلب رقم #{notification.OrderNumber}.",
                $"The vendor started preparing order #{notification.OrderNumber}.",
                NotificationPriorities.Normal,
                OneSignalPushRequestKind.HeadsUp),

                OrderStatus.ReadyForPickup => CreateDriverAssignmentNotification(
                    notification,
                    assignmentId,
                    driverUserId,
                    screen,
                    "assignment.pickup_ready",
                "الطلب جاهز للاستلام",
                "Order ready for pickup",
                $"الطلب رقم #{notification.OrderNumber} أصبح جاهزًا للاستلام من التاجر.",
                $"Order #{notification.OrderNumber} is now ready for pickup.",
                NotificationPriorities.High,
                OneSignalPushRequestKind.HeadsUp),

                OrderStatus.DeliveryFailed => CreateDriverAssignmentNotification(
                    notification,
                    assignmentId,
                    driverUserId,
                    screen,
                    "assignment.delivery_failed",
                "ما قدرنا تسليم الطلب",
                "Delivery failed",
                $"ما قدرنا تسليم الطلب رقم #{notification.OrderNumber}.",
                $"Delivery failed for order #{notification.OrderNumber}.",
                NotificationPriorities.Critical,
                OneSignalPushRequestKind.HeadsUp),

                OrderStatus.Cancelled => CreateDriverAssignmentNotification(
                    notification,
                    assignmentId,
                    driverUserId,
                    screen,
                    "assignment.active_order_cancelled",
                "ألغينا الطلب الحالي",
                "Active order cancelled",
                $"ألغينا الطلب رقم #{notification.OrderNumber}.",
                $"Order #{notification.OrderNumber} was cancelled.",
                NotificationPriorities.Critical,
                OneSignalPushRequestKind.HeadsUp),

            _ => null
        };
    }

    private static DriverAssignmentNotificationEnvelope CreateDriverAssignmentNotification(
        OrderStatusChangedNotification notification,
        Guid assignmentId,
        Guid driverUserId,
        string screen,
        string eventName,
        string titleAr,
        string titleEn,
        string bodyAr,
        string bodyEn,
        string priority,
        OneSignalPushRequestKind pushKind)
    {
        var data = DriverNotificationDataBuilder.Build(
            screen,
            eventName,
            orderId: notification.OrderId,
            assignmentId: assignmentId,
            extra: new
            {
                orderNumber = notification.OrderNumber,
                oldStatus = notification.OldStatus.ToString(),
                newStatus = notification.NewStatus.ToString(),
                actorRole = notification.ActorRole
            });

        var request = new NotificationDispatchRequest(
            titleAr,
            titleEn,
            bodyAr,
            bodyEn,
            NotificationTypes.DriverAssignmentUpdated,
            NotificationCategories.Assignment,
            priority,
            notification.OrderId,
            data);

        var targetUrl = screen switch
        {
            "assignment" or "assignment_detail" => $"/assignments/{assignmentId}",
            "order_detail" or "order_tracking" => $"/orders/{notification.OrderId}",
            _ => "/notifications"
        };

        var pushRequest = pushKind == OneSignalPushRequestKind.HeadsUp
            ? OneSignalMobilePushRequest.CreateHeadsUp(
                driverUserId.ToString(),
                titleAr,
                titleEn,
                bodyAr,
                bodyEn,
                NotificationTypes.DriverAssignmentUpdated,
                notification.OrderId,
                data,
                targetUrl: targetUrl,
                category: NotificationCategories.Assignment,
                targetApplication: OneSignalApplicationTarget.Driver)
            : OneSignalMobilePushRequest.CreateStandard(
                driverUserId.ToString(),
                titleAr,
                titleEn,
                bodyAr,
                bodyEn,
                NotificationTypes.DriverAssignmentUpdated,
                notification.OrderId,
                data,
                targetUrl: targetUrl,
                category: NotificationCategories.Assignment,
                targetApplication: OneSignalApplicationTarget.Driver);

        return new DriverAssignmentNotificationEnvelope(request, pushRequest);
    }

    private static string? ExtractAssignmentEventName(string? data)
    {
        if (string.IsNullOrWhiteSpace(data))
        {
            return null;
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(data);
            if (document.RootElement.TryGetProperty("eventName", out var eventName))
            {
                return eventName.GetString();
            }
        }
        catch
        {
            // Ignore malformed assignment payload when logging.
        }

        return null;
    }


    private sealed record DriverAssignmentNotificationEnvelope(
        NotificationDispatchRequest Request,
        OneSignalMobilePushRequest PushRequest);

    private enum OneSignalPushRequestKind
    {
        Standard,
        HeadsUp
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
                "ألغينا الطلب",
                "Order cancelled",
                $"قام العميل بإلغاء الطلب رقم {orderNumber}.",
                $"The customer cancelled order #{orderNumber}.",
                NotificationTypes.OrderCancelled),
            OrderStatus.DeliveryFailed => (
                "ما قدرنا تسليم الطلب",
                "Delivery failed",
                $"ما قدرنا تسليم الطلب رقم {orderNumber} ويحتاج إلى متابعة.",
                $"Delivery failed for order #{orderNumber} and needs follow-up.",
                NotificationTypes.OrderStatusChanged),
            _ => (
                "تحديث على الطلب",
                "Order update",
                $"حدّثنا الطلب رقم {orderNumber}.",
                $"Order #{orderNumber} has been updated.",
                NotificationTypes.OrderStatusChanged)
        };
    }
}
