using MediatR;
using Zadana.Application.Modules.Catalog.DTOs;

namespace Zadana.Application.Modules.Catalog.Queries.Brands.GetBrandById;

public record GetBrandByIdQuery(Guid BrandId) : IRequest<BrandCustomerDto>;
