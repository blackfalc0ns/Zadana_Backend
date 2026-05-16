using MediatR;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Domain.Modules.Catalog.Enums;

namespace Zadana.Application.Modules.Catalog.Commands.Units.CreateUnit;

public record CreateUnitCommand(
    string NameAr,
    string NameEn,
    string? Symbol,
    UnitKind Kind = UnitKind.Measurement) : IRequest<UnitOfMeasureDto>;
