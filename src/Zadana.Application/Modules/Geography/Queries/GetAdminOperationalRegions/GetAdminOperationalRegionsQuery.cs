using MediatR;
using Zadana.Application.Modules.Geography.DTOs;

namespace Zadana.Application.Modules.Geography.Queries.GetAdminOperationalRegions;

public sealed record GetAdminOperationalRegionsQuery : IRequest<IReadOnlyList<OperationalRegionDto>>;
