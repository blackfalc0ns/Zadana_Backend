using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Delivery.Commands.BlockDriverLocationUpdates;

public record BlockDriverLocationUpdatesCommand(Guid DriverId, Guid AdminUserId, string? Reason) : IRequest;

public class BlockDriverLocationUpdatesCommandHandler : IRequestHandler<BlockDriverLocationUpdatesCommand>
{
    private readonly IDriverRepository _driverRepository;
    private readonly IUnitOfWork _unitOfWork;

    public BlockDriverLocationUpdatesCommandHandler(
        IDriverRepository driverRepository,
        IUnitOfWork unitOfWork)
    {
        _driverRepository = driverRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(BlockDriverLocationUpdatesCommand request, CancellationToken cancellationToken)
    {
        var driver = await _driverRepository.GetByIdAsync(request.DriverId, cancellationToken)
            ?? throw new NotFoundException("Driver", request.DriverId);

        driver.BlockLocationUpdates(request.AdminUserId, request.Reason);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
