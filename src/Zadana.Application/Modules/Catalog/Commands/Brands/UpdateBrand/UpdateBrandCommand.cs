using MediatR;

namespace Zadana.Application.Modules.Catalog.Commands.Brands.UpdateBrand;

public record UpdateBrandCommand(
    Guid Id,
    string NameAr,
    string NameEn,
    string? LogoUrl,
    Guid CategoryId,
    bool IsActive) : IRequest;
