using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
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

    public DriverUpdateOrderStatusCommandHandler(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IPublisher publisher)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _publisher = publisher;
    }

    public async Task<DriverUpdateOrderStatusResultDto> Handle(DriverUpdateOrderStatusCommand request, CancellationToken cancellationToken)
    {
        // Find the order through the delivery assignment
        var assignment = await _context.DeliveryAssignments
            .FirstOrDefaultAsync(x => x.OrderId == request.OrderId && x.DriverId == request.DriverUserId, cancellationToken);

        var order = await _context.Orders
            .Include(x => x.StatusHistory)
            .FirstOrDefaultAsync(x => x.Id == request.OrderId, cancellationToken)
            ?? throw new NotFoundException("Order", request.OrderId);

        if (assignment is null)
        {
            throw new BusinessRuleException("DRIVER_NOT_ASSIGNED", "You are not assigned to this order");
        }

        ValidateTransition(order.Status, request.NewStatus);

        var oldStatus = order.Status;
        order.ChangeStatus(request.NewStatus, request.DriverUserId, request.Note);
        _context.OrderStatusHistories.Add(order.StatusHistory.Last());
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
