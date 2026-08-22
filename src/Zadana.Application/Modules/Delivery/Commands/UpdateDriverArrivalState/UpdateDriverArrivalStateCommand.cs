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
            .Include(item => item.Order)
                .ThenInclude(order => order.StatusHistory)
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
                return await BuildArrivedAtVendorResultAsync(driver.Id, assignment, cancellationToken);
            }

            if (assignment.Status is not Domain.Modules.Delivery.Enums.AssignmentStatus.Accepted)
            {
                throw new BusinessRuleException("INVALID_ARRIVAL_STATE_TRANSITION", "تقدر تسجل الوصول للمتجر فقط بعد قبول الطلب | You can only mark arrival at vendor after accepting the order.");
            }

            assignment.MarkArrivedAtVendor();
            normalizedState = "arrived_at_vendor";
            messageAr = LocalizedMessages.GetAr(LocalizedMessages.DriverArrivedAtVendor);
            messageEn = LocalizedMessages.GetEn(LocalizedMessages.DriverArrivedAtVendor);
            titleAr = "المندوب وصل إلى المتجر";
            titleEn = "Driver arrived at the store";
            bodyAr = $"المندوب وصل لاستلام الطلب {assignment.Order.OrderNumber}.";
            bodyEn = $"The driver has arrived to pick up order #{assignment.Order.OrderNumber}.";
            recipientUserId = assignment.Order.Vendor.UserId;
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

        var updatedDetail = await _driverReadService.GetAssignmentDetailAsync(
            driver.Id, assignment.Id, cancellationToken);

        await NotifyArrivalAsync(
            assignment,
            request.DriverUserId,
            driver.User.FullName,
            recipientUserId,
            normalizedState,
            titleAr,
            titleEn,
            bodyAr,
            bodyEn,
            cancellationToken);

        return new DriverArrivalStateResultDto(
            assignment.OrderId,
            assignment.Id,
            normalizedState,
            messageAr,
            messageEn,
            updatedDetail);
    }

    private async Task<DriverArrivalStateResultDto> BuildArrivedAtVendorResultAsync(
        Guid driverId,
        Domain.Modules.Delivery.Entities.DeliveryAssignment assignment,
        CancellationToken cancellationToken)
    {
        var updatedDetail = await _driverReadService.GetAssignmentDetailAsync(
            driverId,
            assignment.Id,
            cancellationToken);

        return new DriverArrivalStateResultDto(
            assignment.OrderId,
            assignment.Id,
            "arrived_at_vendor",
            LocalizedMessages.GetAr(LocalizedMessages.DriverArrivedAtVendor),
            LocalizedMessages.GetEn(LocalizedMessages.DriverArrivedAtVendor),
            updatedDetail);
    }

    private async Task NotifyArrivalAsync(
        Domain.Modules.Delivery.Entities.DeliveryAssignment assignment,
        Guid driverUserId,
        string driverName,
        Guid recipientUserId,
        string normalizedState,
        string titleAr,
        string titleEn,
        string bodyAr,
        string bodyEn,
        CancellationToken cancellationToken)
    {
        var targetUrl = $"/orders/{assignment.OrderId}";
        var notificationData = BuildArrivalNotificationData(
            assignment.OrderId,
            assignment.Order.OrderNumber,
            normalizedState,
            driverName,
            "driver",
            targetUrl);

        await TryNotifyAsync(
            "vendor-inbox",
            assignment.OrderId,
            ct => _notificationService.SendToUserAsync(
                recipientUserId,
                titleAr,
                titleEn,
                bodyAr,
                bodyEn,
                "driver-arrival",
                assignment.OrderId,
                notificationData,
                ct),
            cancellationToken);

        await TryNotifyAsync(
            "arrival-state",
            assignment.OrderId,
            ct => _notificationService.SendDriverArrivalStateChangedToUserAsync(
                recipientUserId,
                assignment.OrderId,
                assignment.Order.OrderNumber,
                normalizedState,
                driverName,
                "driver",
                targetUrl,
                ct),
            cancellationToken);

        if (normalizedState == "arrived_at_customer")
        {
            await TryNotifyAsync(
                "customer-push",
                assignment.OrderId,
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
                            assignment.OrderId,
                            notificationData,
                            targetUrl,
                            category: NotificationCategories.Order,
                            targetApplication: OneSignalApplicationTarget.Customer),
                        ct);

                    if (pushResult is not null && !pushResult.Sent)
                    {
                        _logger.LogWarning(
                            "Customer driver-arrival push was not sent for order {OrderId} user {UserId}. Attempted: {Attempted}. Skipped: {Skipped}. ProviderStatusCode: {ProviderStatusCode}. Reason: {Reason}",
                            assignment.OrderId,
                            recipientUserId,
                            pushResult.Attempted,
                            pushResult.Skipped,
                            pushResult.ProviderStatusCode,
                            pushResult.Reason);
                    }
                },
                cancellationToken);
        }

        await TryNotifyAsync(
            "tracking-hub",
            assignment.OrderId,
            ct => _orderTrackingRealtimeNotifier.BroadcastDriverArrivalStateAsync(
                assignment.OrderId,
                assignment.Order.OrderNumber,
                normalizedState,
                driverName,
                "driver",
                ct),
            cancellationToken);

        await TryNotifyAsync(
            "assignment-updated",
            assignment.OrderId,
            ct => _notificationService.SendAssignmentUpdatedToDriverAsync(
                driverUserId,
                assignment.Id,
                assignment.OrderId,
                ct),
            cancellationToken);
    }

    private async Task TryNotifyAsync(
        string operation,
        Guid orderId,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(2));

        try
        {
            await action(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
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
        var updatedDetail = await _driverReadService.GetAssignmentDetailAsync(
            driverId,
            assignment.Id,
            cancellationToken);

        return new DriverArrivalStateResultDto(
            assignment.OrderId,
            assignment.Id,
            "arrived_at_customer",
            LocalizedMessages.GetAr(LocalizedMessages.DriverArrivedAtCustomer),
            LocalizedMessages.GetEn(LocalizedMessages.DriverArrivedAtCustomer),
            updatedDetail);
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

        await _notificationService.SendToUserAsync(
            order.UserId,
            "رمز التسليم جاهز",
            "Delivery OTP is ready",
            $"رمز تسليم طلبك رقم {order.OrderNumber} جاهز. افتح تفاصيل الطلب لعرض الرمز المؤمّن عند وصول المندوب.",
            $"Your delivery code for order #{order.OrderNumber} is ready. Open the order details to view it when the driver arrives.",
            "delivery-otp",
            order.Id,
            "otpType=delivery",
            cancellationToken);

        await _oneSignalPushService.SendMobileNotificationDirectAsync(
            OneSignalMobilePushRequest.CreateHeadsUp(
                order.UserId.ToString(),
                "رمز التسليم جاهز",
                "Delivery OTP is ready",
                $"رمز تسليم طلبك رقم {order.OrderNumber} جاهز. افتح تفاصيل الطلب لعرض الرمز المؤمّن عند وصول المندوب.",
                $"Your delivery code for order #{order.OrderNumber} is ready. Open the order details to view it when the driver arrives.",
                "delivery-otp",
                order.Id,
                "otpType=delivery",
                $"/orders/{order.Id}",
                category: NotificationCategories.Order,
                targetApplication: OneSignalApplicationTarget.Customer),
            cancellationToken);

        await _publisher.Publish(
            new OrderStatusChangedNotification(
                order.Id,
                order.UserId,
                order.VendorId,
                order.OrderNumber,
                oldStatus,
                OrderStatus.OnTheWay,
                NotifyCustomer: true,
                NotifyVendor: false,
                ActorRole: "driver"),
            cancellationToken);
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

        await _publisher.Publish(
            new OrderStatusChangedNotification(
                order.Id,
                order.UserId,
                order.VendorId,
                order.OrderNumber,
                oldStatus,
                order.Status,
                NotifyCustomer: true,
                NotifyVendor: false,
                ActorRole: "driver"),
            cancellationToken);
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
