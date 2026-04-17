using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Orders.Events;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Orders.Commands.VendorUpdateOrderStatus;

public record VendorUpdateOrderStatusCommand(
    Guid OrderId,
    Guid VendorId,
    OrderStatus NewStatus,
    string? Note) : IRequest<VendorUpdateOrderStatusResultDto>;

public record VendorUpdateOrderStatusResultDto(Guid OrderId, string Status, string Message);

public class VendorUpdateOrderStatusCommandValidator : AbstractValidator<VendorUpdateOrderStatusCommand>
{
    private static readonly OrderStatus[] AllowedVendorStatuses =
    [
        OrderStatus.Accepted,
        OrderStatus.VendorRejected,
        OrderStatus.Preparing,
        OrderStatus.ReadyForPickup
    ];

    public VendorUpdateOrderStatusCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.VendorId).NotEmpty();
        RuleFor(x => x.NewStatus)
            .Must(status => AllowedVendorStatuses.Contains(status))
            .WithMessage("Vendor can only set status to: Accepted, VendorRejected, Preparing, ReadyForPickup");
    }
}

public class VendorUpdateOrderStatusCommandHandler : IRequestHandler<VendorUpdateOrderStatusCommand, VendorUpdateOrderStatusResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublisher _publisher;

    public VendorUpdateOrderStatusCommandHandler(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IPublisher publisher)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _publisher = publisher;
    }

    public async Task<VendorUpdateOrderStatusResultDto> Handle(VendorUpdateOrderStatusCommand request, CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .Include(x => x.StatusHistory)
            .FirstOrDefaultAsync(x => x.Id == request.OrderId && x.VendorId == request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Order", request.OrderId);

        ValidateTransition(order.Status, request.NewStatus);

        var oldStatus = order.Status;
        order.ChangeStatus(request.NewStatus, null, request.Note);
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
                NotifyVendor: false,
                ActorRole: "vendor"),
            cancellationToken);

        return new VendorUpdateOrderStatusResultDto(
            order.Id,
            request.NewStatus.ToString(),
            "Order status updated successfully");
    }

    private static void ValidateTransition(OrderStatus current, OrderStatus target)
    {
        var valid = (current, target) switch
        {
            (OrderStatus.PendingVendorAcceptance, OrderStatus.Accepted) => true,
            (OrderStatus.PendingVendorAcceptance, OrderStatus.VendorRejected) => true,
            (OrderStatus.Accepted, OrderStatus.Preparing) => true,
            (OrderStatus.Preparing, OrderStatus.ReadyForPickup) => true,
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
