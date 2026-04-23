using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Delivery.Commands.UpdateDriverLocation;

public class UpdateDriverLocationCommandHandler : IRequestHandler<UpdateDriverLocationCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateDriverLocationCommandHandler(IApplicationDbContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(UpdateDriverLocationCommand request, CancellationToken cancellationToken)
    {
        var driver = await _context.Drivers.FindAsync([request.DriverId], cancellationToken)
            ?? throw new NotFoundException("Driver", request.DriverId);

        var location = new DriverLocation(driver.Id, request.Latitude, request.Longitude, request.AccuracyMeters);
        _context.DriverLocations.Add(location);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
