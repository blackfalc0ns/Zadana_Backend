using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.DTOs;

namespace Zadana.Application.Modules.Catalog.Queries.Units.GetUnits;

public class GetUnitsQueryHandler : IRequestHandler<GetUnitsQuery, List<UnitOfMeasureDto>>
{
    private readonly IApplicationDbContext _context;

    public GetUnitsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<UnitOfMeasureDto>> Handle(GetUnitsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.UnitsOfMeasure.AsNoTracking();

        if (!request.IncludeInactive)
        {
            query = query.Where(u => u.IsActive);
        }

        var units = await query
            .OrderBy(u => u.NameEn)
            .Select(u => new UnitOfMeasureDto(
                u.Id,
                u.NameAr,
                u.NameEn,
                u.Symbol,
                u.IsActive))
            .ToListAsync(cancellationToken);

        return units;
    }
}
