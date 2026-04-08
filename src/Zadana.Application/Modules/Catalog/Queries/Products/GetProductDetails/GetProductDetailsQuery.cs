using MediatR;
using Zadana.Application.Modules.Catalog.DTOs;

namespace Zadana.Application.Modules.Catalog.Queries.Products.GetProductDetails;

public record GetProductDetailsQuery(Guid ProductId) : IRequest<ProductDetailsDto>;
