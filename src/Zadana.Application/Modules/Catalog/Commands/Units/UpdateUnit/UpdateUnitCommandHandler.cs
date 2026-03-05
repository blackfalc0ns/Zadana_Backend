using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Commands.Units.UpdateUnit;

public class UpdateUnitCommandHandler : IRequestHandler<UpdateUnitCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdateUnitCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(UpdateUnitCommand request, CancellationToken cancellationToken)
    {
        var unit = await _context.UnitsOfMeasure.FindAsync(new object[] { request.Id }, cancellationToken);
        if (unit == null)
            throw new NotFoundException(nameof(UnitOfMeasure), request.Id);

        unit.Update(request.NameAr, request.NameEn, request.Symbol);

        if (request.IsActive && !unit.IsActive)
            unit.Activate();
        else if (!request.IsActive && unit.IsActive)
            unit.Deactivate();

        await _context.SaveChangesAsync(cancellationToken);
    }
}
