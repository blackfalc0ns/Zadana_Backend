using Zadana.Domain.Modules.Catalog.Enums;

namespace Zadana.Application.Modules.Catalog.DTOs;

public record UnitOfMeasureDto(
    Guid Id,
    string NameAr,
    string NameEn,
    string? Symbol,
    UnitKind Kind,
    bool IsActive);
