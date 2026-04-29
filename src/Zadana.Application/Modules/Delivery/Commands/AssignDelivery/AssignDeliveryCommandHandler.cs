using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Orders.Events;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Delivery.Commands.AssignDelivery;

public class AssignDeliveryCommandHandler : IRequestHandler<AssignDeliveryCommand, Guid>
{
    private static readonly TimeSpan PickupOtpTtl = TimeSpan.FromHours(12);

    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublisher _publisher;

    public AssignDeliveryCommandHandler(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IPublisher publisher)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _publisher = publisher;
    }

    public async Task<Guid> Handle(AssignDeliveryCommand request, CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .Include(x => x.StatusHistory)
            .FirstOrDefaultAsync(x => x.Id == request.OrderId, cancellationToken)
            ?? throw new NotFoundException("Order", request.OrderId);

        var driver = await _context.Drivers
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == request.DriverId, cancellationToken)
            ?? throw new NotFoundException("Driver", request.DriverId);

        // Find or create assignment
        var assignment = await _context.DeliveryAssignments
            .FirstOrDefaultAsync(x => x.OrderId == order.Id, cancellationToken);

        if (assignment is null)
        {
            assignment = new DeliveryAssignment(
                order.Id,
                ResolveCodAmount(order));
            _context.DeliveryAssignments.Add(assignment);
        }
        else
        {
            assignment.UpdateCodAmount(ResolveCodAmount(order));
        }

        var oldStatus = order.Status;

        assignment.OfferTo(driver.Id, assignment.DispatchAttemptNumber + 1, DateTime.UtcNow.AddMinutes(5));
        assignment.Accept();

        // Generate pickup OTP — same as auto-dispatch
        assignment.EnsurePickupOtp(PickupOtpTtl);

        order.ChangeStatus(OrderStatus.DriverAssigned, null, "Driver assigned via dispatch");
        _context.OrderStatusHistories.Add(order.StatusHistory.Last());

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Publish lifecycle event — handles customer + vendor notifications
        await _publisher.Publish(
            new OrderStatusChangedNotification(
                order.Id,
                order.UserId,
                order.VendorId,
                order.OrderNumber,
                oldStatus,
                OrderStatus.DriverAssigned,
                NotifyCustomer: true,
                NotifyVendor: true,
                ActorRole: "system"),
            cancellationToken);

        return assignment.Id;
    }

    private static decimal ResolveCodAmount(Zadana.Domain.Modules.Orders.Entities.Order order) =>
        order.PaymentMethod == PaymentMethodType.CashOnDelivery ? order.TotalAmount : 0m;
}
