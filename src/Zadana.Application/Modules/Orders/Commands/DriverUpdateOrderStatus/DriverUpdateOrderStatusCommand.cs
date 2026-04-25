using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Orders.Events;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Orders.Commands.DriverUpdateOrderStatus;

public record DriverUpdateOrderStatusCommand(
    Guid OrderId,
    Guid DriverUserId,
    OrderStatus NewStatus,
    string? Note) : IRequest<DriverUpdateOrderStatusResultDto>;

public record DriverUpdateOrderStatusResultDto(Guid OrderId, string Status, string Message);

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

    public DriverUpdateOrderStatusCommandHandler(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IPublisher publisher,
        IDriverRepository driverRepository,
        INotificationService notificationService)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _publisher = publisher;
        _driverRepository = driverRepository;
        _notificationService = notificationService;
    }

    public async Task<DriverUpdateOrderStatusResultDto> Handle(DriverUpdateOrderStatusCommand request, CancellationToken cancellationToken)
    {
        // BUG FIX: Resolve Driver.Id from the current user's UserId first
        var driver = await _driverRepository.GetByUserIdAsync(request.DriverUserId, cancellationToken)
            ?? throw new BusinessRuleException("DRIVER_NOT_FOUND", "No driver profile found for the current user");

        if (!driver.CanReceiveOrders)
        {
            throw new BusinessRuleException(
                "DRIVER_NOT_READY_FOR_DISPATCH",
                "Driver must be reviewed and approved by admin before handling delivery orders.");
        }

        // Now compare using the actual Driver.Id against the assignment
        var assignment = await _context.DeliveryAssignments
            .FirstOrDefaultAsync(x => x.OrderId == request.OrderId && x.DriverId == driver.Id, cancellationToken);

        var order = await _context.Orders
            .Include(x => x.StatusHistory)
            .FirstOrDefaultAsync(x => x.Id == request.OrderId, cancellationToken)
            ?? throw new NotFoundException("Order", request.OrderId);

        if (assignment is null)
        {
            throw new BusinessRuleException("DRIVER_NOT_ASSIGNED", "You are not assigned to this order");
        }

        if (request.NewStatus == OrderStatus.PickedUp && !assignment.IsPickupOtpVerified)
        {
            throw new BusinessRuleException(
                "PICKUP_OTP_REQUIRED",
                "Pickup OTP must be verified before marking the order as picked up.");
        }

        if (request.NewStatus == OrderStatus.Delivered && !assignment.IsDeliveryOtpVerified)
        {
            throw new BusinessRuleException(
                "DELIVERY_OTP_REQUIRED",
                "Delivery OTP must be verified before marking the order as delivered.");
        }

        if (request.NewStatus == OrderStatus.DeliveryFailed && string.IsNullOrWhiteSpace(request.Note))
        {
            throw new BusinessRuleException(
                "DELIVERY_FAILURE_NOTE_REQUIRED",
                "A failure note is required before marking delivery as failed.");
        }

        ValidateTransition(order.Status, request.NewStatus);

        var oldStatus = order.Status;
        order.ChangeStatus(request.NewStatus, request.DriverUserId, request.Note);
        _context.OrderStatusHistories.Add(order.StatusHistory.Last());

        string? deliveryOtp = null;
        if (request.NewStatus == OrderStatus.OnTheWay)
        {
            deliveryOtp = assignment.EnsureDeliveryOtp(DeliveryOtpTtl);
        }

        // Update assignment status to match order status
        if (request.NewStatus == OrderStatus.PickedUp)
            assignment.MarkPickedUp();
        else if (request.NewStatus == OrderStatus.Delivered)
            assignment.MarkDelivered();
        else if (request.NewStatus == OrderStatus.DeliveryFailed)
            assignment.Fail(request.Note ?? "Delivery failed");

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(deliveryOtp))
        {
            await _notificationService.SendToUserAsync(
                order.UserId,
                "رمز التسليم جاهز",
                "Delivery OTP is ready",
                $"رمز تسليم طلبك رقم {order.OrderNumber} هو {deliveryOtp}. شاركه مع المندوب عند الاستلام.",
                $"Your delivery OTP for order #{order.OrderNumber} is {deliveryOtp}. Share it with the driver on arrival.",
                "delivery-otp",
                order.Id,
                $"deliveryOtp={deliveryOtp}",
                cancellationToken);
        }

        // Publish notification event
        await _publisher.Publish(
            new OrderStatusChangedNotification(
                order.Id,
                order.UserId,
                order.VendorId,
                order.OrderNumber,
                oldStatus,
                request.NewStatus,
                NotifyCustomer: true,
                NotifyVendor: request.NewStatus is OrderStatus.DeliveryFailed or OrderStatus.Delivered,
                ActorRole: "driver"),
            cancellationToken);

        return new DriverUpdateOrderStatusResultDto(
            order.Id,
            request.NewStatus.ToString(),
            "Order status updated successfully");
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
                $"Cannot transition from {current} to {target}");
        }
    }
}
