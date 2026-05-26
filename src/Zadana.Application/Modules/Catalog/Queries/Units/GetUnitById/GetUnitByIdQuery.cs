using MediatR;
using Zadana.Application.Modules.Catalog.DTOs;

namespace Zadana.Application.Modules.Catalog.Queries.Units.GetUnitById;

public record GetUnitByIdQuery(Guid Id) : IRequest<UnitOfMeasureDto>;
