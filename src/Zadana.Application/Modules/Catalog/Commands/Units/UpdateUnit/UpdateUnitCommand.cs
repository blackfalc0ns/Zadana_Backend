using MediatR;

namespace Zadana.Application.Modules.Catalog.Commands.Units.UpdateUnit;

public record UpdateUnitCommand(
    Guid Id,
    string NameAr,
    string NameEn,
    string? Symbol,
    bool IsActive) : IRequest;
