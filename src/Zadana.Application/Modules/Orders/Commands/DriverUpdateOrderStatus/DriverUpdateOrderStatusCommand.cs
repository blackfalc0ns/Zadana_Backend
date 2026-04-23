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
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublisher _publisher;
    private readonly IDriverRepository _driverRepository;

    public DriverUpdateOrderStatusCommandHandler(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IPublisher publisher,
        IDriverRepository driverRepository)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _publisher = publisher;
        _driverRepository = driverRepository;
    }

    public async Task<DriverUpdateOrderStatusResultDto> Handle(DriverUpdateOrderStatusCommand request, CancellationToken cancellationToken)
    {
        // BUG FIX: Resolve Driver.Id from the current user's UserId first
        var driver = await _driverRepository.GetByUserIdAsync(request.DriverUserId, cancellationToken)
            ?? throw new BusinessRuleException("DRIVER_NOT_FOUND", "No driver profile found for the current user");

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

        if (request.NewStatus == OrderStatus.Delivered)
        {
            var hasPhotoProof = await _context.DeliveryProofs
                .AnyAsync(
                    proof => proof.AssignmentId == assignment.Id &&
                        (!string.IsNullOrWhiteSpace(proof.ImageUrl) ||
                         proof.ProofType == "Image" ||
                         proof.ProofType == "Photo"),
                    cancellationToken);

            if (!hasPhotoProof)
            {
                throw new BusinessRuleException(
                    "DELIVERY_PROOF_REQUIRED",
                    "Photo proof is required before marking an order as delivered.");
            }
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

        // Update assignment status to match order status
        if (request.NewStatus == OrderStatus.PickedUp)
            assignment.MarkPickedUp();
        else if (request.NewStatus == OrderStatus.Delivered)
            assignment.MarkDelivered();
        else if (request.NewStatus == OrderStatus.DeliveryFailed)
            assignment.Fail(request.Note ?? "Delivery failed");

        await _unitOfWork.SaveChangesAsync(cancellationToken);

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
                NotifyVendor: request.NewStatus == OrderStatus.DeliveryFailed,
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
