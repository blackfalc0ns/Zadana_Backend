using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Orders.Events;
using Zadana.Application.Modules.Orders.Services;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Domain.Modules.Social.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Orders.Commands.DriverUpdateOrderStatus;

public record DriverUpdateOrderStatusCommand(
    Guid OrderId,
    Guid DriverUserId,
    OrderStatus NewStatus,
    string? Note) : IRequest<DriverUpdateOrderStatusResultDto>;

public record DriverUpdateOrderStatusResultDto(
    Guid OrderId,
    string Status,
    string MessageAr,
    string MessageEn,
    Delivery.DTOs.DriverAssignmentDetailDto? UpdatedAssignment = null);

public class DriverUpdateOrderStatusCommandValidator : AbstractValidator<DriverUpdateOrderStatusCommand>
{
    private static readonly OrderStatus[] AllowedDriverStatuses =
    [
        OrderStatus.PickedUp,
        OrderStatus.OnTheWay,
        OrderStatus.Delivered,
        OrderStatus.DeliveryFailed
    ];

    public DriverUpdateOrderStatusCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.DriverUserId).NotEmpty();
        RuleFor(x => x.NewStatus)
            .Must(status => AllowedDriverStatuses.Contains(status))
            .WithMessage("Driver can only set status to: PickedUp, OnTheWay, Delivered, DeliveryFailed");
    }
}

public class DriverUpdateOrderStatusCommandHandler : IRequestHandler<DriverUpdateOrderStatusCommand, DriverUpdateOrderStatusResultDto>
{
    private static readonly TimeSpan DeliveryOtpTtl = TimeSpan.FromHours(12);

    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublisher _publisher;
    private readonly IDriverRepository _driverRepository;
    private readonly INotificationService _notificationService;
    private readonly IOneSignalPushService _oneSignalPushService;
    private readonly OrderInventoryWorkflowService _orderInventoryWorkflowService;
    private readonly ILogger<DriverUpdateOrderStatusCommandHandler> _logger;

    public DriverUpdateOrderStatusCommandHandler(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IPublisher publisher,
        IDriverRepository driverRepository,
        IDriverReadService driverReadService,
        INotificationService notificationService,
        IOneSignalPushService oneSignalPushService,
        OrderInventoryWorkflowService orderInventoryWorkflowService,
        ILogger<DriverUpdateOrderStatusCommandHandler> logger)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _publisher = publisher;
        _driverRepository = driverRepository;
        _notificationService = notificationService;
        _oneSignalPushService = oneSignalPushService;
        _orderInventoryWorkflowService = orderInventoryWorkflowService;
        _logger = logger;
        // driverReadService kept in ctor for DI compatibility with existing registrations/tests;
        // assignment detail is no longer loaded on the hot path.
        _ = driverReadService;
    }

    public async Task<DriverUpdateOrderStatusResultDto> Handle(DriverUpdateOrderStatusCommand request, CancellationToken cancellationToken)
    {
        var driver = await _driverRepository.GetByUserIdAsync(request.DriverUserId, cancellationToken)
            ?? throw new BusinessRuleException("DRIVER_NOT_FOUND", "ما لقينا حساب مندوب مرتبط بهذا المستخدم | No driver profile found for the current user.");

        if (driver.ApplyDocumentExpiryLock())
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        if (!driver.CanReceiveOrders)
        {
            throw new BusinessRuleException(
                "DRIVER_NOT_READY_FOR_DISPATCH",
                "تحتاج مراجعة واعتماد من الإدارة قبل البدء بتوصيل الطلبات | Your account must be reviewed and approved by admin before handling deliveries.");
        }

        var assignment = await _context.DeliveryAssignments
            .FirstOrDefaultAsync(x => x.OrderId == request.OrderId && x.DriverId == driver.Id, cancellationToken);

        // Do not Include StatusHistory — loading it on this hot path hung the same way as arrived-at-vendor.
        var order = await _context.Orders
            .FirstOrDefaultAsync(x => x.Id == request.OrderId, cancellationToken)
            ?? throw new NotFoundException("Order", request.OrderId);

        if (assignment is null)
        {
            throw new BusinessRuleException("DRIVER_NOT_ASSIGNED", "أنت غير مخصص لهذا الطلب | You are not assigned to this order.");
        }

        if (IsAlreadyCompletedTransition(order.Status, assignment, request.NewStatus))
        {
            return new DriverUpdateOrderStatusResultDto(
                order.Id,
                request.NewStatus.ToString(),
                LocalizedMessages.GetAr(LocalizedMessages.OrderStatusUpdated),
                LocalizedMessages.GetEn(LocalizedMessages.OrderStatusUpdated));
        }

        if (request.NewStatus == OrderStatus.PickedUp && !assignment.IsPickupOtpVerified)
        {
            throw new BusinessRuleException(
                "PICKUP_OTP_REQUIRED",
                "لازم تأكد رمز الاستلام من المتجر قبل تحديث حالة الطلب | Pickup OTP must be verified by the vendor before marking the order as picked up.");
        }

        if (request.NewStatus == OrderStatus.Delivered && !assignment.IsDeliveryOtpVerified)
        {
            throw new BusinessRuleException(
                "DELIVERY_OTP_REQUIRED",
                "لازم تأكد رمز التوصيل من العميل قبل إتمام الطلب | Delivery OTP must be verified by the customer before completing delivery.");
        }

        if (request.NewStatus == OrderStatus.DeliveryFailed && string.IsNullOrWhiteSpace(request.Note))
        {
            throw new BusinessRuleException(
                "DELIVERY_FAILURE_NOTE_REQUIRED",
                "لازم تكتب سبب فشل التوصيل قبل تسجيله | A note explaining the delivery failure reason is required.");
        }

        ValidateTransition(order.Status, request.NewStatus);

        var oldStatus = order.Status;
        order.ChangeStatus(request.NewStatus, request.DriverUserId, request.Note);
        _context.OrderStatusHistories.Add(order.StatusHistory.Last());

        var deliveryOtpReady = false;
        if (request.NewStatus is OrderStatus.PickedUp or OrderStatus.OnTheWay)
        {
            assignment.EnsureDeliveryOtp(DeliveryOtpTtl);
            deliveryOtpReady = true;
        }

        if (request.NewStatus == OrderStatus.PickedUp)
        {
            assignment.MarkPickedUp();
            await _orderInventoryWorkflowService.ApplyPickupDeductionAsync(order.Id, cancellationToken);
        }
        else if (request.NewStatus == OrderStatus.Delivered)
        {
            assignment.MarkDelivered();

            if (order.PaymentMethod == PaymentMethodType.CashOnDelivery)
            {
                order.UpdatePaymentStatus(PaymentStatus.Paid);

                var codPayment = await _context.Payments
                    .OrderByDescending(p => p.CreatedAtUtc)
                    .FirstOrDefaultAsync(p => p.OrderId == order.Id, cancellationToken);

                codPayment?.MarkAsPaid();
            }
        }
        else if (request.NewStatus == OrderStatus.DeliveryFailed)
        {
            assignment.Fail(request.Note ?? "Delivery failed");
            await _orderInventoryWorkflowService.ApplyRestockAsync(order.Id, "delivery_failed", cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        QueuePostPersistNotifications(
            order.Id,
            order.UserId,
            order.VendorId,
            order.OrderNumber,
            oldStatus,
            request.NewStatus,
            deliveryOtpReady);

        return new DriverUpdateOrderStatusResultDto(
            order.Id,
            request.NewStatus.ToString(),
            LocalizedMessages.GetAr(LocalizedMessages.OrderStatusUpdated),
            LocalizedMessages.GetEn(LocalizedMessages.OrderStatusUpdated));
    }

    private void QueuePostPersistNotifications(
        Guid orderId,
        Guid customerUserId,
        Guid vendorId,
        string orderNumber,
        OrderStatus oldStatus,
        OrderStatus newStatus,
        bool deliveryOtpReady)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                if (deliveryOtpReady)
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
                        NotifyVendor: newStatus is OrderStatus.DeliveryFailed or OrderStatus.Delivered,
                        ActorRole: "driver"),
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Driver status post-persist fan-out failed for order {OrderId}.", orderId);
            }
        });
    }

    private static void ValidateTransition(OrderStatus current, OrderStatus target)
    {
        var valid = (current, target) switch
        {
            (OrderStatus.DriverAssigned, OrderStatus.PickedUp) => true,
            (OrderStatus.PickedUp, OrderStatus.OnTheWay) => true,
            (OrderStatus.OnTheWay, OrderStatus.Delivered) => true,
            (OrderStatus.OnTheWay, OrderStatus.DeliveryFailed) => true,
            (OrderStatus.DriverAssigned, OrderStatus.DeliveryFailed) => true,
            _ => false
        };

        if (!valid)
        {
            throw new BusinessRuleException(
                "INVALID_ORDER_STATUS_TRANSITION",
                $"ما تقدر تغيّر حالة الطلب من {current} إلى {target} | Cannot transition order from {current} to {target}.");
        }
    }

    private static bool IsAlreadyCompletedTransition(
        OrderStatus orderStatus,
        Domain.Modules.Delivery.Entities.DeliveryAssignment assignment,
        OrderStatus requestedStatus) =>
        requestedStatus switch
        {
            OrderStatus.PickedUp => orderStatus == OrderStatus.PickedUp &&
                                    assignment.IsPickupOtpVerified &&
                                    assignment.Status == Domain.Modules.Delivery.Enums.AssignmentStatus.PickedUp,
            OrderStatus.OnTheWay => orderStatus == OrderStatus.OnTheWay,
            OrderStatus.Delivered => orderStatus == OrderStatus.Delivered &&
                                     assignment.IsDeliveryOtpVerified &&
                                     assignment.Status == Domain.Modules.Delivery.Enums.AssignmentStatus.Delivered,
            _ => false
        };
}
