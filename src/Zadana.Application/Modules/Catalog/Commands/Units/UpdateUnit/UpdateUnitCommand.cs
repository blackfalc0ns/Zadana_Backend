using MediatR;
using Zadana.Domain.Modules.Catalog.Enums;

namespace Zadana.Application.Modules.Catalog.Commands.Units.UpdateUnit;

public record UpdateUnitCommand(
    Guid Id,
    string NameAr,
    string NameEn,
    string? Symbol,
    UnitKind Kind,
    bool IsActive) : IRequest
{
    public UpdateUnitCommand(Guid id, string nameAr, string nameEn, string? symbol, bool isActive)
        : this(id, nameAr, nameEn, symbol, UnitKind.Measurement, isActive)
    {
    }
}
