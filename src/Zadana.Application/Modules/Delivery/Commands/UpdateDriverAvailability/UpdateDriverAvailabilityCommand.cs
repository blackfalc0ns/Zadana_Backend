using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Delivery.Commands.UpdateDriverAvailability;

public record UpdateDriverAvailabilityCommand(Guid DriverUserId, bool IsAvailable) : IRequest;

public class UpdateDriverAvailabilityCommandHandler : IRequestHandler<UpdateDriverAvailabilityCommand>
{
    private readonly IDriverRepository _driverRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateDriverAvailabilityCommandHandler(IDriverRepository driverRepository, IUnitOfWork unitOfWork)
    {
        _driverRepository = driverRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UpdateDriverAvailabilityCommand request, CancellationToken cancellationToken)
    {
        var driver = await _driverRepository.GetByUserIdAsync(request.DriverUserId, cancellationToken)
            ?? throw new NotFoundException("Driver", request.DriverUserId);

        if (request.IsAvailable && !driver.CanReceiveOrders)
        {
            throw new BusinessRuleException(
                "DRIVER_NOT_READY_FOR_DISPATCH",
                "Only approved active drivers can enable availability.");
        }

        driver.ToggleAvailability(request.IsAvailable);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
