using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Delivery.Commands.UnblockDriverLocationUpdates;

public record UnblockDriverLocationUpdatesCommand(Guid DriverId) : IRequest;

public class UnblockDriverLocationUpdatesCommandHandler : IRequestHandler<UnblockDriverLocationUpdatesCommand>
{
    private readonly IDriverRepository _driverRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UnblockDriverLocationUpdatesCommandHandler(
        IDriverRepository driverRepository,
        IUnitOfWork unitOfWork)
    {
        _driverRepository = driverRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UnblockDriverLocationUpdatesCommand request, CancellationToken cancellationToken)
    {
        var driver = await _driverRepository.GetByIdAsync(request.DriverId, cancellationToken)
            ?? throw new NotFoundException("Driver", request.DriverId);

        driver.UnblockLocationUpdates();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
