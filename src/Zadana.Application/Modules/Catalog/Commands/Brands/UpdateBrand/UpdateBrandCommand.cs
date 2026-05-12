using MediatR;

namespace Zadana.Application.Modules.Catalog.Commands.Brands.UpdateBrand;

public record UpdateBrandCommand(
    Guid Id,
    string NameAr,
    string NameEn,
    string? LogoUrl,
    string? CoverImageUrl,
    Guid CategoryId,
    IReadOnlyList<Guid>? CategoryIds,
    bool IsActive) : IRequest;
