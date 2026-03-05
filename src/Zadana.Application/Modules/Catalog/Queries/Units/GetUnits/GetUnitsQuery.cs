using MediatR;
using Zadana.Application.Modules.Catalog.DTOs;

namespace Zadana.Application.Modules.Catalog.Queries.Units.GetUnits;

public record GetUnitsQuery(bool IncludeInactive = false) : IRequest<List<UnitOfMeasureDto>>;
