using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Domain.Modules.Catalog.Entities;

namespace Zadana.Application.Modules.Catalog.Commands.Units.CreateUnit;

public class CreateUnitCommandHandler : IRequestHandler<CreateUnitCommand, UnitOfMeasureDto>
{
    private readonly IApplicationDbContext _context;

    public CreateUnitCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<UnitOfMeasureDto> Handle(CreateUnitCommand request, CancellationToken cancellationToken)
    {
        var unit = new UnitOfMeasure(request.NameAr, request.NameEn, request.Symbol);

        _context.UnitsOfMeasure.Add(unit);
        await _context.SaveChangesAsync(cancellationToken);

        return new UnitOfMeasureDto(
            unit.Id,
            unit.NameAr,
            unit.NameEn,
            unit.Symbol,
            unit.IsActive);
    }
}
