using MediatR;
using Zadana.Application.Modules.Catalog.DTOs;

namespace Zadana.Application.Modules.Catalog.Commands.Brands.CreateBrand;

public record CreateBrandCommand(
    string NameAr,
    string NameEn,
    string? LogoUrl) : IRequest<BrandDto>;
