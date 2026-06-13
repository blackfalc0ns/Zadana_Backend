using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Orders.Events;
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
        _logger = logger;
    }

    public async Task<DriverArrivalStateResultDto> Handle(UpdateDriverArrivalStateCommand request, CancellationToken cancellationToken)
    {
        var driver = await _driverRepository.GetByUserIdAsync(request.DriverUserId, cancellationToken)
            ?? throw new BusinessRuleException("DRIVER_NOT_FOUND", "لم يتم العثور على حساب مندوب مرتبط بهذا المستخدم | No driver profile found for the current user.");

        if (!driver.CanReceiveOrders)
        {
            throw new BusinessRuleException(
                "DRIVER_NOT_READY_FOR_DISPATCH",
                "يجب مراجعة حسابك والموافقة عليه من الإدارة قبل تحديث حالة الوصول | Your account must be reviewed and approved by admin before updating arrival state.");
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
            if (assignment.Status is not Domain.Modules.Delivery.Enums.AssignmentStatus.Accepted)
            {
                throw new BusinessRuleException("INVALID_ARRIVAL_STATE_TRANSITION", "يمكنك تسجيل الوصول للمتجر فقط بعد قبول الطلب | You can only mark arrival at vendor after accepting the order.");
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
                throw new BusinessRuleException(
                    assignment.Status is AssignmentStatus.ArrivedAtVendor or AssignmentStatus.Accepted
                        ? "PICKUP_REQUIRED_BEFORE_CUSTOMER_ARRIVAL"
                        : "INVALID_ARRIVAL_STATE_TRANSITION",
                    assignment.Status is AssignmentStatus.ArrivedAtVendor or AssignmentStatus.Accepted
                        ? "يجب استلام الطلب من المتجر قبل تسجيل الوصول للعميل | Pick up the order from the store before marking arrival at the customer."
                        : "يمكنك تسجيل الوصول للعميل فقط بعد بدء التوصيل | You can only mark arrival at customer after the order is on the way.");
            }

            if (assignment.Order.Status == OrderStatus.PickedUp)
            {
                await PromoteOrderToOnTheWayAsync(assignment, request.DriverUserId, cancellationToken);
            }
            else if (assignment.Order.Status != OrderStatus.OnTheWay)
            {
                throw new BusinessRuleException(
                    "INVALID_ARRIVAL_STATE_TRANSITION",
                    "يمكنك تسجيل الوصول للعميل فقط بعد بدء التوصيل | You can only mark arrival at customer after the order is on the way.");
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

        var targetUrl = $"/orders/{assignment.OrderId}";
        var notificationData = BuildArrivalNotificationData(
            assignment.OrderId,
            assignment.Order.OrderNumber,
            normalizedState,
            driver.User.FullName,
            "driver",
            targetUrl);

        await _notificationService.SendToUserAsync(
            recipientUserId,
            titleAr,
            titleEn,
            bodyAr,
            bodyEn,
            "driver-arrival",
            assignment.OrderId,
            notificationData,
            cancellationToken);

        await _notificationService.SendDriverArrivalStateChangedToUserAsync(
            recipientUserId,
            assignment.OrderId,
            assignment.Order.OrderNumber,
            normalizedState,
            driver.User.FullName,
            "driver",
            targetUrl,
            cancellationToken);

        if (normalizedState == "arrived_at_customer")
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
                cancellationToken);

            if (!pushResult.Sent)
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
        }

        // Push the same arrival state event to the dedicated order tracking channel
        // so any party (admin / vendor / customer / driver) subscribed to the order
        // gets the update without going through their personal user feed.
        await _orderTrackingRealtimeNotifier.BroadcastDriverArrivalStateAsync(
            assignment.OrderId,
            assignment.Order.OrderNumber,
            normalizedState,
            driver.User.FullName,
            "driver",
            cancellationToken);

        // Push full assignment detail to the driver so their order detail screen refreshes in real-time
        await _notificationService.SendAssignmentUpdatedToDriverAsync(
            request.DriverUserId,
            assignment.Id,
            assignment.OrderId,
            cancellationToken);


        // Fetch the full updated assignment detail so mobile can refresh UI immediately
        var updatedDetail = await _driverReadService.GetAssignmentDetailAsync(
            driver.Id, assignment.Id, cancellationToken);

        return new DriverArrivalStateResultDto(
            assignment.OrderId,
            assignment.Id,
            normalizedState,
            messageAr,
            messageEn,
            updatedDetail);
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
