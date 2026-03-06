using MediatR;
using Zadana.Application.Common.Models;
using Zadana.Application.Modules.Catalog.DTOs;

namespace Zadana.Application.Modules.Catalog.Queries.GetMasterProducts;

public record GetMasterProductsQuery(
    string? SearchTerm,
    Guid? CategoryId,
    int PageNumber = 1,
    int PageSize = 10) : IRequest<PaginatedList<MasterProductDto>>;
