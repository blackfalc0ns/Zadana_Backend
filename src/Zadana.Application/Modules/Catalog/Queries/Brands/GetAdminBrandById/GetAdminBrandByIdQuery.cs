using MediatR;
using Zadana.Application.Modules.Catalog.DTOs;

namespace Zadana.Application.Modules.Catalog.Queries.Brands.GetAdminBrandById;

public record GetAdminBrandByIdQuery(Guid BrandId) : IRequest<BrandDto>;
