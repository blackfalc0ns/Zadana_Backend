using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Delivery.Commands.SetDriverZone;

public record SetDriverZoneCommand(Guid DriverUserId, Guid ZoneId) : IRequest;

public class SetDriverZoneCommandHandler : IRequestHandler<SetDriverZoneCommand>
{
    private readonly IDriverRepository _driverRepository;
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public SetDriverZoneCommandHandler(
        IDriverRepository driverRepository,
        IApplicationDbContext context,
        IUnitOfWork unitOfWork)
    {
        _driverRepository = driverRepository;
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(SetDriverZoneCommand request, CancellationToken cancellationToken)
    {
        var driver = await _driverRepository.GetByUserIdAsync(request.DriverUserId, cancellationToken)
            ?? throw new NotFoundException("Driver", request.DriverUserId);

        var zoneExists = await _context.DeliveryZones
            .AnyAsync(z => z.Id == request.ZoneId && z.IsActive, cancellationToken);

        if (!zoneExists)
            throw new NotFoundException("DeliveryZone", request.ZoneId);

        driver.AssignZone(request.ZoneId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
