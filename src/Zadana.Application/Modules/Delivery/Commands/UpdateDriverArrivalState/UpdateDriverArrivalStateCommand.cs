using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Orders.Events;
using Zadana.Application.Modules.Orders.Services;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Social.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Delivery.Commands.UpdateDriverArrivalState;

public record UpdateDriverArrivalStateCommand(
    Guid OrderId,
    Guid DriverUserId,
    string ArrivalState) : IRequest<DriverArrivalStateResultDto>;

public record DriverArrivalStateResultDto(
    Guid OrderId,
    Guid AssignmentId,
    string ArrivalState,
    string MessageAr,
    string MessageEn,
    DTOs.DriverAssignmentDetailDto? UpdatedAssignment = null);

public class UpdateDriverArrivalStateCommandValidator : AbstractValidator<UpdateDriverArrivalStateCommand>
{
    public UpdateDriverArrivalStateCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.DriverUserId).NotEmpty();
        RuleFor(x => x.ArrivalState)
            .Must(value => value.Equals("arrived_at_vendor", StringComparison.OrdinalIgnoreCase) ||
                           value.Equals("arrived_at_customer", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Arrival state must be arrived_at_vendor or arrived_at_customer.");
    }
}

public class UpdateDriverArrivalStateCommandHandler : IRequestHandler<UpdateDriverArrivalStateCommand, DriverArrivalStateResultDto>
{
    private static readonly TimeSpan DeliveryOtpTtl = TimeSpan.FromHours(12);

    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDriverRepository _driverRepository;
    private readonly IDriverReadService _driverReadService;
    private readonly INotificationService _notificationService;
    private readonly IOneSignalPushService _oneSignalPushService;
    private readonly IOrderTrackingRealtimeNotifier _orderTrackingRealtimeNotifier;
    private readonly IPublisher _publisher;
    private readonly OrderInventoryWorkflowService _orderInventoryWorkflowService;
    private readonly ILogger<UpdateDriverArrivalStateCommandHandler> _logger;

    public UpdateDriverArrivalStateCommandHandler(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IDriverRepository driverRepository,
        IDriverReadService driverReadService,
        INotificationService notificationService,
        IOneSignalPushService oneSignalPushService,
        IOrderTrackingRealtimeNotifier orderTrackingRealtimeNotifier,
        IPublisher publisher,
        OrderInventoryWorkflowService orderInventoryWorkflowService,
        ILogger<UpdateDriverArrivalStateCommandHandler> logger)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _driverRepository = driverRepository;
        _driverReadService = driverReadService;
        _notificationService = notificationService;
        _oneSignalPushService = oneSignalPushService;
        _orderTrackingRealtimeNotifier = orderTrackingRealtimeNotifier;
        _publisher = publisher;
        _orderInventoryWorkflowService = orderInventoryWorkflowService;
        _logger = logger;
    }

    public async Task<DriverArrivalStateResultDto> Handle(UpdateDriverArrivalStateCommand request, CancellationToken cancellationToken)
    {
        var driver = await _driverRepository.GetByUserIdAsync(request.DriverUserId, cancellationToken)
            ?? throw new BusinessRuleException("DRIVER_NOT_FOUND", "ما لقينا حساب مندوب مرتبط بهذا المستخدم | No driver profile found for the current user.");

        if (!driver.CanReceiveOrders)
        {
            throw new BusinessRuleException(
                "DRIVER_NOT_READY_FOR_DISPATCH",
                "تحتاج مراجعة واعتماد من الإدارة قبل تحديث حالة الوصول | Your account must be reviewed and approved by admin before updating arrival state.");
        }

        var assignment = await _context.DeliveryAssignments
            .Include(item => item.Order)
                .ThenInclude(order => order.Vendor)
            .FirstOrDefaultAsync(item => item.OrderId == request.OrderId && item.DriverId == driver.Id, cancellationToken)
            ?? throw new BusinessRuleException("DRIVER_NOT_ASSIGNED", "أنت غير مخصص لهذا الطلب | You are not assigned to this order.");

        string normalizedState;
        string messageAr;
        string messageEn;
        string titleAr;
        string titleEn;
        string bodyAr;
        string bodyEn;
        Guid recipientUserId;

        if (request.ArrivalState.Equals("arrived_at_vendor", StringComparison.OrdinalIgnoreCase))
        {
            if (assignment.Status == AssignmentStatus.ArrivedAtVendor)
            {
                return new DriverArrivalStateResultDto(
                    assignment.OrderId,
                    assignment.Id,
                    "arrived_at_vendor",
                    LocalizedMessages.GetAr(LocalizedMessages.DriverArrivedAtVendor),
                    LocalizedMessages.GetEn(LocalizedMessages.DriverArrivedAtVendor));
            }

            if (assignment.Status is not AssignmentStatus.Accepted)
            {
                throw new BusinessRuleException("INVALID_ARRIVAL_STATE_TRANSITION", "تقدر تسجل الوصول للمتجر فقط بعد قبول الطلب | You can only mark arrival at vendor after accepting the order.");
            }

            var arrivedAtUtc = DateTime.UtcNow;
            if (_context is DbContext dbContext &&
                string.Equals(
                    dbContext.Database.ProviderName,
                    "Microsoft.EntityFrameworkCore.InMemory",
                    StringComparison.Ordinal))
            {
                assignment.MarkArrivedAtVendor();
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            else
            {
                var updatedRows = await _context.DeliveryAssignments
                    .Where(item =>
                        item.Id == assignment.Id &&
                        item.Status == AssignmentStatus.Accepted)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(item => item.Status, AssignmentStatus.ArrivedAtVendor)
                            .SetProperty(item => item.ArrivedAtVendorAtUtc, arrivedAtUtc)
                            .SetProperty(item => item.UpdatedAtUtc, arrivedAtUtc),
                        cancellationToken);

                if (updatedRows != 1)
                {
                    throw new BusinessRuleException(
                        "ARRIVAL_STATE_UPDATE_CONFLICT",
                        "تعذر تسجيل الوصول لأن حالة الطلب تغيرت، حدّث الصفحة وحاول مرة أخرى | The assignment changed while recording arrival. Refresh and try again.");
                }
            }

            const string vendorArrivalState = "arrived_at_vendor";
            var vendorMessageAr = LocalizedMessages.GetAr(LocalizedMessages.DriverArrivedAtVendor);
            var vendorMessageEn = LocalizedMessages.GetEn(LocalizedMessages.DriverArrivedAtVendor);

            QueueArrivalNotifications(
                assignment.OrderId,
                assignment.Order.OrderNumber,
                assignment.Id,
                request.DriverUserId,
                driver.User.FullName,
                assignment.Order.Vendor.UserId,
                vendorArrivalState,
                "المندوب وصل إلى المتجر",
                "Driver arrived at the store",
                $"المندوب وصل لاستلام الطلب {assignment.Order.OrderNumber}.",
                $"The driver has arrived to pick up order #{assignment.Order.OrderNumber}.");

            return new DriverArrivalStateResultDto(
                assignment.OrderId,
                assignment.Id,
                vendorArrivalState,
                vendorMessageAr,
                vendorMessageEn);
        }
        else
        {
            if (assignment.Status == AssignmentStatus.ArrivedAtCustomer)
            {
                return await BuildArrivedAtCustomerResultAsync(driver.Id, assignment, cancellationToken);
            }

            if (assignment.Status != AssignmentStatus.PickedUp)
            {
                if (assignment.Status == AssignmentStatus.ArrivedAtVendor && assignment.IsPickupOtpVerified)
                {
                    await CompletePickupHandoffAsync(assignment, request.DriverUserId, cancellationToken);
                }
                else
                {
                    throw new BusinessRuleException(
                        ResolveCustomerArrivalBlockErrorCode(assignment),
                        ResolveCustomerArrivalBlockMessage(assignment));
                }
            }

            if (assignment.Order.Status == OrderStatus.PickedUp)
            {
                await PromoteOrderToOnTheWayAsync(assignment, request.DriverUserId, cancellationToken);
            }
            else if (assignment.Order.Status != OrderStatus.OnTheWay)
            {
                throw new BusinessRuleException(
                    "INVALID_ARRIVAL_STATE_TRANSITION",
                    "تقدر تسجل الوصول للعميل فقط بعد بدء التوصيل | You can only mark arrival at customer after the order is on the way.");
            }

            assignment.MarkArrivedAtCustomer();
            normalizedState = "arrived_at_customer";
            messageAr = LocalizedMessages.GetAr(LocalizedMessages.DriverArrivedAtCustomer);
            messageEn = LocalizedMessages.GetEn(LocalizedMessages.DriverArrivedAtCustomer);
            titleAr = "المندوب وصل إلى موقع التسليم";
            titleEn = "Driver arrived at delivery location";
            bodyAr = $"المندوب وصل إليك بطلب {assignment.Order.OrderNumber}. جهز رمز التسليم.";
            bodyEn = $"The driver has arrived with order #{assignment.Order.OrderNumber}. Please prepare your delivery OTP.";
            recipientUserId = assignment.Order.UserId;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        QueueArrivalNotifications(
            assignment.OrderId,
            assignment.Order.OrderNumber,
            assignment.Id,
            request.DriverUserId,
            driver.User.FullName,
            recipientUserId,
            normalizedState,
            titleAr,
            titleEn,
            bodyAr,
            bodyEn);

        return new DriverArrivalStateResultDto(
            assignment.OrderId,
            assignment.Id,
            normalizedState,
            messageAr,
            messageEn);
    }

    private void QueueArrivalNotifications(
        Guid orderId,
        string orderNumber,
        Guid assignmentId,
        Guid driverUserId,
        string driverName,
        Guid recipientUserId,
        string normalizedState,
        string titleAr,
        string titleEn,
        string bodyAr,
        string bodyEn)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await NotifyArrivalAsync(
                    orderId,
                    orderNumber,
                    assignmentId,
                    driverUserId,
                    driverName,
                    recipientUserId,
                    normalizedState,
                    titleAr,
                    titleEn,
                    bodyAr,
                    bodyEn);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Arrival notification fan-out failed for order {OrderId}.", orderId);
            }
        });
    }

    private async Task NotifyArrivalAsync(
        Guid orderId,
        string orderNumber,
        Guid assignmentId,
        Guid driverUserId,
        string driverName,
        Guid recipientUserId,
        string normalizedState,
        string titleAr,
        string titleEn,
        string bodyAr,
        string bodyEn)
    {
        var targetUrl = $"/orders/{orderId}";
        var notificationData = BuildArrivalNotificationData(
            orderId,
            orderNumber,
            normalizedState,
            driverName,
            "driver",
            targetUrl);

        await TryNotifyAsync(
            "vendor-inbox",
            orderId,
            ct => _notificationService.SendToUserAsync(
                recipientUserId,
                titleAr,
                titleEn,
                bodyAr,
                bodyEn,
                "driver-arrival",
                orderId,
                notificationData,
                ct));

        await TryNotifyAsync(
            "arrival-state",
            orderId,
            ct => _notificationService.SendDriverArrivalStateChangedToUserAsync(
                recipientUserId,
                orderId,
                orderNumber,
                normalizedState,
                driverName,
                "driver",
                targetUrl,
                ct));

        if (normalizedState == "arrived_at_customer")
        {
            await TryNotifyAsync(
                "customer-push",
                orderId,
                async ct =>
                {
                    var pushResult = await _oneSignalPushService.SendMobileNotificationDirectAsync(
                        OneSignalMobilePushRequest.CreateHeadsUp(
                            recipientUserId.ToString(),
                            titleAr,
                            titleEn,
                            bodyAr,
                            bodyEn,
                            "driver-arrival",
                            orderId,
                            notificationData,
                            targetUrl,
                            category: NotificationCategories.Order,
                            targetApplication: OneSignalApplicationTarget.Customer),
                        ct);

                    if (pushResult is not null && !pushResult.Sent)
                    {
                        _logger.LogWarning(
                            "Customer driver-arrival push was not sent for order {OrderId} user {UserId}. Attempted: {Attempted}. Skipped: {Skipped}. ProviderStatusCode: {ProviderStatusCode}. Reason: {Reason}",
                            orderId,
                            recipientUserId,
                            pushResult.Attempted,
                            pushResult.Skipped,
                            pushResult.ProviderStatusCode,
                            pushResult.Reason);
                    }
                });
        }

        await TryNotifyAsync(
            "tracking-hub",
            orderId,
            ct => _orderTrackingRealtimeNotifier.BroadcastDriverArrivalStateAsync(
                orderId,
                orderNumber,
                normalizedState,
                driverName,
                "driver",
                ct));

        await TryNotifyAsync(
            "assignment-updated",
            orderId,
            ct => _notificationService.SendAssignmentUpdatedToDriverAsync(
                driverUserId,
                assignmentId,
                orderId,
                ct));
    }

    private async Task TryNotifyAsync(
        string operation,
        Guid orderId,
        Func<CancellationToken, Task> action)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        try
        {
            await action(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Arrival notification {Operation} timed out for order {OrderId}; continuing remaining events.",
                operation,
                orderId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Arrival notification {Operation} failed for order {OrderId}; continuing remaining events.",
                operation,
                orderId);
        }
    }

    private async Task<DriverArrivalStateResultDto> BuildArrivedAtCustomerResultAsync(
        Guid driverId,
        Domain.Modules.Delivery.Entities.DeliveryAssignment assignment,
        CancellationToken cancellationToken)
    {
        // Persist-only idempotent response — avoid GetAssignmentDetail on the hot path.
        _ = driverId;
        _ = cancellationToken;

        return new DriverArrivalStateResultDto(
            assignment.OrderId,
            assignment.Id,
            "arrived_at_customer",
            LocalizedMessages.GetAr(LocalizedMessages.DriverArrivedAtCustomer),
            LocalizedMessages.GetEn(LocalizedMessages.DriverArrivedAtCustomer));
    }

    private async Task PromoteOrderToOnTheWayAsync(
        Domain.Modules.Delivery.Entities.DeliveryAssignment assignment,
        Guid driverUserId,
        CancellationToken cancellationToken)
    {
        var order = assignment.Order;
        var oldStatus = order.Status;
        order.ChangeStatus(OrderStatus.OnTheWay, driverUserId, "Driver is on the way.");
        _context.OrderStatusHistories.Add(order.StatusHistory.Last());
        assignment.EnsureDeliveryOtp(DeliveryOtpTtl);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        QueueOrderStatusSideEffects(
            order.Id,
            order.UserId,
            order.VendorId,
            order.OrderNumber,
            oldStatus,
            OrderStatus.OnTheWay,
            notifyDeliveryOtp: true);
    }

    private async Task CompletePickupHandoffAsync(
        Domain.Modules.Delivery.Entities.DeliveryAssignment assignment,
        Guid driverUserId,
        CancellationToken cancellationToken)
    {
        var order = assignment.Order;
        var oldStatus = order.Status;

        if (order.Status != OrderStatus.PickedUp)
        {
            order.ChangeStatus(OrderStatus.PickedUp, driverUserId, "Pickup confirmed before customer arrival.");
            _context.OrderStatusHistories.Add(order.StatusHistory.Last());
        }

        if (assignment.Status != AssignmentStatus.PickedUp)
        {
            assignment.MarkPickedUp();
        }

        await _orderInventoryWorkflowService.ApplyPickupDeductionAsync(order.Id, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        QueueOrderStatusSideEffects(
            order.Id,
            order.UserId,
            order.VendorId,
            order.OrderNumber,
            oldStatus,
            order.Status,
            notifyDeliveryOtp: false);
    }

    private void QueueOrderStatusSideEffects(
        Guid orderId,
        Guid customerUserId,
        Guid vendorId,
        string orderNumber,
        OrderStatus oldStatus,
        OrderStatus newStatus,
        bool notifyDeliveryOtp)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                if (notifyDeliveryOtp)
                {
                    // Persist inbox only — SendToUserAsync also emits ReceiveNotification and duplicates the push banner.
                    await _notificationService.PersistToUserAsync(
                        customerUserId,
                        "رمز التسليم جاهز",
                        "Delivery OTP is ready",
                        $"رمز تسليم طلبك رقم {orderNumber} جاهز. افتح تفاصيل الطلب لعرض الرمز المؤمّن عند وصول المندوب.",
                        $"Your delivery code for order #{orderNumber} is ready. Open the order details to view it when the driver arrives.",
                        "delivery-otp",
                        orderId,
                        "otpType=delivery",
                        CancellationToken.None);

                    await _oneSignalPushService.SendMobileNotificationDirectAsync(
                        OneSignalMobilePushRequest.CreateHeadsUp(
                            customerUserId.ToString(),
                            "رمز التسليم جاهز",
                            "Delivery OTP is ready",
                            $"رمز تسليم طلبك رقم {orderNumber} جاهز. افتح تفاصيل الطلب لعرض الرمز المؤمّن عند وصول المندوب.",
                            $"Your delivery code for order #{orderNumber} is ready. Open the order details to view it when the driver arrives.",
                            "delivery-otp",
                            orderId,
                            "otpType=delivery",
                            $"/orders/{orderId}",
                            category: NotificationCategories.Order,
                            targetApplication: OneSignalApplicationTarget.Customer),
                        CancellationToken.None);
                }

                await _publisher.Publish(
                    new OrderStatusChangedNotification(
                        orderId,
                        customerUserId,
                        vendorId,
                        orderNumber,
                        oldStatus,
                        newStatus,
                        NotifyCustomer: oldStatus != newStatus,
                        NotifyVendor: false,
                        ActorRole: "driver"),
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Customer-arrival status side effects failed for order {OrderId}.", orderId);
            }
        });
    }

    private static string ResolveCustomerArrivalBlockErrorCode(
        Domain.Modules.Delivery.Entities.DeliveryAssignment assignment) =>
        assignment.Status switch
        {
            AssignmentStatus.Accepted => "VENDOR_ARRIVAL_REQUIRED",
            AssignmentStatus.ArrivedAtVendor when assignment.RequiresPickupOtpVerification => "PICKUP_OTP_PENDING",
            AssignmentStatus.ArrivedAtVendor or AssignmentStatus.Accepted => "PICKUP_REQUIRED_BEFORE_CUSTOMER_ARRIVAL",
            _ => "INVALID_ARRIVAL_STATE_TRANSITION"
        };

    private static string ResolveCustomerArrivalBlockMessage(
        Domain.Modules.Delivery.Entities.DeliveryAssignment assignment) =>
        assignment.Status switch
        {
            AssignmentStatus.Accepted =>
                "لازم تسجل الوصول للمتجر واستلام الطلب قبل الوصول للعميل | Mark arrival at the store and pick up the order before arriving at the customer.",
            AssignmentStatus.ArrivedAtVendor when assignment.RequiresPickupOtpVerification =>
                "لازم تأكد رمز الاستلام من المتجر قبل تسجيل الوصول للعميل | Confirm the store pickup OTP before marking arrival at the customer.",
            AssignmentStatus.ArrivedAtVendor or AssignmentStatus.Accepted =>
                "لازم تستلم الطلب من المتجر قبل تسجيل الوصول للعميل | Pick up the order from the store before marking arrival at the customer.",
            _ =>
                "تقدر تسجل الوصول للعميل فقط بعد بدء التوصيل | You can only mark arrival at customer after the order is on the way."
        };

    private static string BuildArrivalNotificationData(
        Guid orderId,
        string orderNumber,
        string arrivalState,
        string driverName,
        string actorRole,
        string targetUrl) =>
        JsonSerializer.Serialize(new
        {
            orderId,
            orderNumber,
            arrivalState,
            driverName,
            actorRole,
            targetUrl,
            category = "order",
            screen = "order_tracking",
            presentation = "popup",
            popupType = "driver_arrival_state_changed",
            showPopup = true,
            eventName = $"driver.arrival.{arrivalState}"
        });
}
