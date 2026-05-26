using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Queries.Units.GetUnitById;

public class GetUnitByIdQueryHandler : IRequestHandler<GetUnitByIdQuery, UnitOfMeasureDto>
{
    private readonly IApplicationDbContext _context;

    public GetUnitByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<UnitOfMeasureDto> Handle(GetUnitByIdQuery request, CancellationToken cancellationToken)
    {
        var unit = await _context.UnitsOfMeasure
            .AsNoTracking()
            .Where(u => u.Id == request.Id)
            .Select(u => new UnitOfMeasureDto(
                u.Id,
                u.NameAr,
                u.NameEn,
                u.Symbol,
                u.Kind,
                u.IsActive))
            .FirstOrDefaultAsync(cancellationToken);

        if (unit is null)
        {
            throw new NotFoundException("UnitOfMeasure", request.Id);
        }

        return unit;
    }
}
