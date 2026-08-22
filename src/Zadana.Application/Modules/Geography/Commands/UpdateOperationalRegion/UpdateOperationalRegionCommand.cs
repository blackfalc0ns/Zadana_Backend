using MediatR;
using Zadana.Application.Modules.Geography.DTOs;

namespace Zadana.Application.Modules.Geography.Commands.UpdateOperationalRegion;

public sealed record UpdateOperationalRegionCommand(
    string RegionCode,
    bool IsOperational) : IRequest<OperationalRegionDto>;
