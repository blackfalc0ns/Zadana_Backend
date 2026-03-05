using MediatR;
using Zadana.Application.Modules.Catalog.DTOs;

namespace Zadana.Application.Modules.Catalog.Commands.Units.CreateUnit;

public record CreateUnitCommand(
    string NameAr,
    string NameEn,
    string? Symbol) : IRequest<UnitOfMeasureDto>;
