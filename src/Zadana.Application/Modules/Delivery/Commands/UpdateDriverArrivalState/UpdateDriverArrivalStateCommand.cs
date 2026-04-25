using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.Interfaces;
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
    string Message);

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
    private readonly INotificationService _notificationService;

    public UpdateDriverArrivalStateCommandHandler(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IDriverRepository driverRepository,
        INotificationService notificationService)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _driverRepository = driverRepository;
        _notificationService = notificationService;
    }

    public async Task<DriverArrivalStateResultDto> Handle(UpdateDriverArrivalStateCommand request, CancellationToken cancellationToken)
    {
        var driver = await _driverRepository.GetByUserIdAsync(request.DriverUserId, cancellationToken)
            ?? throw new BusinessRuleException("DRIVER_NOT_FOUND", "No driver profile found for the current user.");

        if (!driver.CanReceiveOrders)
        {
            throw new BusinessRuleException(
                "DRIVER_NOT_READY_FOR_DISPATCH",
                "Driver must be reviewed and approved by admin before updating arrival state.");
        }

        var assignment = await _context.DeliveryAssignments
            .Include(item => item.Order)
                .ThenInclude(order => order.Vendor)
            .FirstOrDefaultAsync(item => item.OrderId == request.OrderId && item.DriverId == driver.Id, cancellationToken)
            ?? throw new BusinessRuleException("DRIVER_NOT_ASSIGNED", "You are not assigned to this order.");

        string normalizedState;
        string message;
        string titleAr;
        string titleEn;
        string bodyAr;
        string bodyEn;
        Guid recipientUserId;

        if (request.ArrivalState.Equals("arrived_at_vendor", StringComparison.OrdinalIgnoreCase))
        {
            if (assignment.Status is not Domain.Modules.Delivery.Enums.AssignmentStatus.Accepted)
            {
                throw new BusinessRuleException("INVALID_ARRIVAL_STATE_TRANSITION", "Driver can only arrive at vendor after accepting the order.");
            }

            assignment.MarkArrivedAtVendor();
            normalizedState = "arrived_at_vendor";
            message = "Driver arrival at vendor recorded successfully.";
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
                throw new BusinessRuleException("INVALID_ARRIVAL_STATE_TRANSITION", "Driver can only arrive at customer after the order is on the way.");
            }

            assignment.MarkArrivedAtCustomer();
            normalizedState = "arrived_at_customer";
            message = "Driver arrival at customer recorded successfully.";
            titleAr = "المندوب وصل إلى موقع التسليم";
            titleEn = "Driver arrived at delivery location";
            bodyAr = $"المندوب وصل إليك بطلب {assignment.Order.OrderNumber}. جهز رمز التسليم.";
            bodyEn = $"The driver has arrived with order #{assignment.Order.OrderNumber}. Please prepare your delivery OTP.";
            recipientUserId = assignment.Order.UserId;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _notificationService.SendToUserAsync(
            recipientUserId,
            titleAr,
            titleEn,
            bodyAr,
            bodyEn,
            "driver-arrival",
            assignment.OrderId,
            $"arrivalState={normalizedState}",
            cancellationToken);

        await _notificationService.SendDriverArrivalStateChangedToUserAsync(
            recipientUserId,
            assignment.OrderId,
            assignment.Order.OrderNumber,
            normalizedState,
            driver.User.FullName,
            "driver",
            $"/orders/{assignment.OrderId}",
            cancellationToken);

        return new DriverArrivalStateResultDto(
            assignment.OrderId,
            assignment.Id,
            normalizedState,
            message);
    }
}
