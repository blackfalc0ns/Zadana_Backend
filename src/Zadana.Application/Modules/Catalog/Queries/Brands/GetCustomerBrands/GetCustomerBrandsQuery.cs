using MediatR;
using Zadana.Application.Modules.Catalog.DTOs;

namespace Zadana.Application.Modules.Catalog.Queries.Brands.GetCustomerBrands;

public record GetCustomerBrandsQuery() : IRequest<List<BrandCustomerDto>>;
