using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Delivery.Commands.AssignDelivery;

public class AssignDeliveryCommandHandler : IRequestHandler<AssignDeliveryCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public AssignDeliveryCommandHandler(IApplicationDbContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(AssignDeliveryCommand request, CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .FirstOrDefaultAsync(x => x.Id == request.OrderId, cancellationToken)
            ?? throw new NotFoundException("Order", request.OrderId);

        var driver = await _context.Drivers
            .FirstOrDefaultAsync(x => x.Id == request.DriverId, cancellationToken)
            ?? throw new NotFoundException("Driver", request.DriverId);

        // Find or create assignment
        var assignment = await _context.DeliveryAssignments
            .FirstOrDefaultAsync(x => x.OrderId == order.Id, cancellationToken);

        if (assignment is null)
        {
            assignment = new DeliveryAssignment(
                order.Id,
                request.CodAmount);
            _context.DeliveryAssignments.Add(assignment);
        }

        assignment.OfferTo(driver.Id, assignment.DispatchAttemptNumber + 1, DateTime.UtcNow.AddMinutes(5));
        assignment.Accept();

        order.ChangeStatus(OrderStatus.DriverAssigned, null, "Driver assigned via dispatch");

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return assignment.Id;
    }
}
