using MediatR;
using Zadana.Application.Modules.Geography.DTOs;

namespace Zadana.Application.Modules.Geography.Commands.UpdateOperationalCity;

public sealed record UpdateOperationalCityCommand(
    string CityCode,
    bool IsOperational) : IRequest<OperationalCityDto>;
