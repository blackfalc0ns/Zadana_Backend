using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Orders.Events;
using Zadana.Application.Modules.Orders.Services;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Orders.Commands.ConfirmVendorPickupOtp;

public record ConfirmVendorPickupOtpCommand(
    Guid OrderId,
    Guid VendorId,
    Guid? BranchId,
    string OtpCode) : IRequest<VendorPickupOtpConfirmationDto>;

public record VendorPickupOtpConfirmationDto(
    Guid OrderId,
    Guid AssignmentId,
    string Status,
    string Message);

public class ConfirmVendorPickupOtpCommandValidator : AbstractValidator<ConfirmVendorPickupOtpCommand>
{
    public ConfirmVendorPickupOtpCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.VendorId).NotEmpty();
        RuleFor(x => x.OtpCode).NotEmpty().MaximumLength(10);
    }
}

public class ConfirmVendorPickupOtpCommandHandler : IRequestHandler<ConfirmVendorPickupOtpCommand, VendorPickupOtpConfirmationDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublisher _publisher;
    private readonly OrderInventoryWorkflowService _orderInventoryWorkflowService;

    public ConfirmVendorPickupOtpCommandHandler(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IPublisher publisher,
        OrderInventoryWorkflowService orderInventoryWorkflowService)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _publisher = publisher;
        _orderInventoryWorkflowService = orderInventoryWorkflowService;
    }

    public async Task<VendorPickupOtpConfirmationDto> Handle(ConfirmVendorPickupOtpCommand request, CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .FirstOrDefaultAsync(item =>
                item.Id == request.OrderId &&
                item.VendorId == request.VendorId &&
                (!request.BranchId.HasValue || item.VendorBranchId == request.BranchId.Value),
                cancellationToken)
            ?? throw new NotFoundException("Order", request.OrderId);

        var assignment = await _context.DeliveryAssignments
            .Where(item =>
                item.OrderId == order.Id &&
                item.DriverId != null &&
                (item.Status == AssignmentStatus.Accepted ||
                 item.Status == AssignmentStatus.ArrivedAtVendor ||
                 item.Status == AssignmentStatus.PickedUp ||
                 item.Status == AssignmentStatus.ArrivedAtCustomer))
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new BusinessRuleException("NO_ASSIGNED_DRIVER", "No assigned driver was found for this order.");

        if (!assignment.DriverId.HasValue)
        {
            throw new BusinessRuleException("NO_ASSIGNED_DRIVER", "No assigned driver was found for this order.");
        }

        if (assignment.IsPickupOtpVerified &&
            order.Status == OrderStatus.PickedUp &&
            assignment.Status == AssignmentStatus.PickedUp)
        {
            return new VendorPickupOtpConfirmationDto(
                order.Id,
                assignment.Id,
                "picked_up",
                "Order was already handed off to the driver.");
        }

        if (order.Status is not (OrderStatus.DriverAssigned or OrderStatus.PickedUp))
        {
            throw new BusinessRuleException(
                "INVALID_ORDER_STATUS_TRANSITION",
                $"Cannot confirm pickup OTP while order is in {order.Status}.");
        }

        try
        {
            assignment.VerifyPickupOtp(assignment.DriverId.Value, request.OtpCode);
        }
        catch (InvalidOperationException ex)
        {
            throw new BusinessRuleException("PICKUP_OTP_INVALID", ex.Message);
        }

        var oldStatus = order.Status;
        if (order.Status != OrderStatus.PickedUp)
        {
            order.ChangeStatus(OrderStatus.PickedUp, null, "Vendor confirmed pickup handoff via OTP.");
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
                ActorRole: "vendor"),
            cancellationToken);

        return new VendorPickupOtpConfirmationDto(
            order.Id,
            assignment.Id,
            "picked_up",
            "Pickup OTP confirmed and order handed off to the driver.");
    }
}
