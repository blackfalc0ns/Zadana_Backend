using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.EmailCenter;
using Zadana.Application.Modules.EmailCenter.DTOs;
using Zadana.Application.Modules.EmailCenter.Interfaces;
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
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDriverRepository _driverRepository;
    private readonly IDriverReadService _driverReadService;
    private readonly INotificationService _notificationService;
    private readonly IOneSignalPushService _oneSignalPushService;
    private readonly IOrderTrackingRealtimeNotifier _orderTrackingRealtimeNotifier;
    private readonly IEmailCenterService _emailCenterService;
    private readonly ILogger<UpdateDriverArrivalStateCommandHandler> _logger;

    public UpdateDriverArrivalStateCommandHandler(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IDriverRepository driverRepository,
        IDriverReadService driverReadService,
        INotificationService notificationService,
        IOneSignalPushService oneSignalPushService,
        IOrderTrackingRealtimeNotifier orderTrackingRealtimeNotifier,
        IEmailCenterService emailCenterService,
        ILogger<UpdateDriverArrivalStateCommandHandler> logger)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _driverRepository = driverRepository;
        _driverReadService = driverReadService;
        _notificationService = notificationService;
        _oneSignalPushService = oneSignalPushService;
        _orderTrackingRealtimeNotifier = orderTrackingRealtimeNotifier;
        _emailCenterService = emailCenterService;
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
            if (assignment.Order.Status != Domain.Modules.Orders.Enums.OrderStatus.OnTheWay ||
                assignment.Status is not (Domain.Modules.Delivery.Enums.AssignmentStatus.PickedUp or Domain.Modules.Delivery.Enums.AssignmentStatus.ArrivedAtCustomer))
            {
                throw new BusinessRuleException("INVALID_ARRIVAL_STATE_TRANSITION", "يمكنك تسجيل الوصول للعميل فقط بعد بدء التوصيل | You can only mark arrival at customer after the order is on the way.");
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
            await OneSignalMobilePushRequest.CreateHeadsUp(
                    recipientUserId.ToString(),
                    titleAr,
                    titleEn,
                    bodyAr,
                    bodyEn,
                    "driver-arrival",
                    assignment.OrderId,
                    notificationData,
                    targetUrl,
                    category: "order")
                .DispatchAsync(_oneSignalPushService, cancellationToken);

            await DispatchCustomerArrivalEmailAsync(assignment.OrderId, assignment.Order.OrderNumber, assignment.Order.VendorId, cancellationToken);
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

    private async Task DispatchCustomerArrivalEmailAsync(
        Guid orderId,
        string orderNumber,
        Guid vendorId,
        CancellationToken cancellationToken)
    {
        try
        {
            var order = await _context.Orders
                .AsNoTracking()
                .Where(item => item.Id == orderId)
                .Select(item => new
                {
                    CustomerName = item.User.FullName,
                    CustomerEmail = item.User.Email,
                    VendorName = string.IsNullOrWhiteSpace(item.Vendor.BusinessNameEn)
                        ? item.Vendor.BusinessNameAr
                        : item.Vendor.BusinessNameEn
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (order is null || string.IsNullOrWhiteSpace(order.CustomerEmail))
            {
                return;
            }

            await _emailCenterService.DispatchSystemEventEmailAsync(
                new EmailSystemEventDispatchRequest(
                    EventKey: EmailEventKeys.CustomerDriverArrivedAtDelivery,
                    AudienceType: "customers",
                    To: [order.CustomerEmail.Trim()],
                    Variables: new Dictionary<string, string>
                    {
                        ["customer_name"] = string.IsNullOrWhiteSpace(order.CustomerName) ? "Customer" : order.CustomerName,
                        ["order_number"] = orderNumber,
                        ["vendor_name"] = order.VendorName
                    },
                    TargetUrl: $"/orders/{orderId}",
                    EntityId: orderId,
                    VendorId: vendorId),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Customer driver-arrival email dispatch failed for order {OrderId}", orderId);
        }
    }
}
