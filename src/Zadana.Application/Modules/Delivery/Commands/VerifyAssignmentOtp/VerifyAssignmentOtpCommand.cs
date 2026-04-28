using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Orders.Events;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Delivery.Commands.VerifyAssignmentOtp;

public record VerifyAssignmentOtpCommand(
    Guid AssignmentId,
    Guid DriverUserId,
    string OtpType,
    string OtpCode) : IRequest<DriverOtpVerificationResultDto>;

public class VerifyAssignmentOtpCommandValidator : AbstractValidator<VerifyAssignmentOtpCommand>
{
    public VerifyAssignmentOtpCommandValidator()
    {
        RuleFor(x => x.AssignmentId).NotEmpty();
        RuleFor(x => x.DriverUserId).NotEmpty();
        RuleFor(x => x.OtpCode).NotEmpty().MaximumLength(10);
        RuleFor(x => x.OtpType)
            .Must(value => value.Equals("pickup", StringComparison.OrdinalIgnoreCase) || value.Equals("delivery", StringComparison.OrdinalIgnoreCase))
            .WithMessage("OTP type must be pickup or delivery.");
    }
}

public class VerifyAssignmentOtpCommandHandler : IRequestHandler<VerifyAssignmentOtpCommand, DriverOtpVerificationResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDriverRepository _driverRepository;
    private readonly IPublisher _publisher;

    public VerifyAssignmentOtpCommandHandler(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IDriverRepository driverRepository,
        IPublisher publisher)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _driverRepository = driverRepository;
        _publisher = publisher;
    }

    public async Task<DriverOtpVerificationResultDto> Handle(VerifyAssignmentOtpCommand request, CancellationToken cancellationToken)
    {
        var driver = await _driverRepository.GetByUserIdAsync(request.DriverUserId, cancellationToken)
            ?? throw new BusinessRuleException("DRIVER_NOT_FOUND", "No driver profile found for the current user.");

        if (!driver.CanReceiveOrders)
        {
            throw new BusinessRuleException(
                "DRIVER_NOT_READY_FOR_DISPATCH",
                "Driver must be reviewed and approved by admin before verifying delivery OTP.");
        }

        var assignment = await _context.DeliveryAssignments
            .Include(item => item.Order)
            .FirstOrDefaultAsync(item => item.Id == request.AssignmentId && item.DriverId == driver.Id, cancellationToken)
            ?? throw new BusinessRuleException("ASSIGNMENT_NOT_OWNED", "You can only verify OTP for your assigned delivery.");

        var otpType = request.OtpType.Trim().ToLowerInvariant();

        if (otpType == "pickup" &&
            assignment.IsPickupOtpVerified &&
            assignment.Order.Status == OrderStatus.PickedUp &&
            assignment.Status == AssignmentStatus.PickedUp)
        {
            return new DriverOtpVerificationResultDto(
                assignment.Id,
                assignment.OrderId,
                otpType,
                "picked_up",
                "Pickup OTP was already verified and the order is already picked up.");
        }

        if (otpType == "delivery" &&
            assignment.IsDeliveryOtpVerified &&
            assignment.Order.Status == OrderStatus.Delivered &&
            assignment.Status == AssignmentStatus.Delivered)
        {
            return new DriverOtpVerificationResultDto(
                assignment.Id,
                assignment.OrderId,
                otpType,
                "delivered",
                "Delivery OTP was already verified and the order is already delivered.");
        }

        if (otpType == "pickup" && assignment.Order.Status is not (OrderStatus.DriverAssigned or OrderStatus.PickedUp))
        {
            throw new BusinessRuleException(
                "INVALID_ORDER_STATUS_TRANSITION",
                $"Cannot verify pickup OTP while order is in {assignment.Order.Status}.");
        }

        if (otpType == "delivery" && assignment.Order.Status is not (OrderStatus.OnTheWay or OrderStatus.Delivered))
        {
            throw new BusinessRuleException(
                "INVALID_ORDER_STATUS_TRANSITION",
                $"Cannot verify delivery OTP while order is in {assignment.Order.Status}.");
        }

        try
        {
            if (otpType == "pickup")
            {
                assignment.VerifyPickupOtp(driver.Id, request.OtpCode);
            }
            else
            {
                assignment.VerifyDeliveryOtp(driver.Id, request.OtpCode);
            }
        }
        catch (InvalidOperationException ex)
        {
            var errorCode = request.OtpType.Equals("pickup", StringComparison.OrdinalIgnoreCase)
                ? "PICKUP_OTP_INVALID"
                : "DELIVERY_OTP_INVALID";

            throw new BusinessRuleException(errorCode, ex.Message);
        }

        var oldStatus = assignment.Order.Status;
        string status;
        string message;

        if (otpType == "pickup")
        {
            if (assignment.Order.Status != OrderStatus.PickedUp)
            {
                assignment.Order.ChangeStatus(OrderStatus.PickedUp, request.DriverUserId, "Driver verified pickup OTP.");
                _context.OrderStatusHistories.Add(assignment.Order.StatusHistory.Last());
            }

            if (assignment.Status != AssignmentStatus.PickedUp)
            {
                assignment.MarkPickedUp();
            }

            status = "picked_up";
            message = "Pickup OTP verified and order marked as picked up.";
        }
        else
        {
            if (assignment.Order.Status != OrderStatus.Delivered)
            {
                assignment.Order.ChangeStatus(OrderStatus.Delivered, request.DriverUserId, "Driver verified delivery OTP.");
                _context.OrderStatusHistories.Add(assignment.Order.StatusHistory.Last());
            }

            if (assignment.Status != AssignmentStatus.Delivered)
            {
                assignment.MarkDelivered();
            }

            status = "delivered";
            message = "Delivery OTP verified and order marked as delivered.";
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _publisher.Publish(
            new OrderStatusChangedNotification(
                assignment.OrderId,
                assignment.Order.UserId,
                assignment.Order.VendorId,
                assignment.Order.OrderNumber,
                oldStatus,
                assignment.Order.Status,
                NotifyCustomer: true,
                NotifyVendor: assignment.Order.Status is OrderStatus.Delivered or OrderStatus.DeliveryFailed,
                ActorRole: "driver"),
            cancellationToken);

        return new DriverOtpVerificationResultDto(
            assignment.Id,
            assignment.OrderId,
            otpType,
            status,
            message);
    }
}
