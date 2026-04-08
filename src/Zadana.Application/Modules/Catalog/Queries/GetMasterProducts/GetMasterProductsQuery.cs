using MediatR;
using Zadana.Application.Common.Models;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Domain.Modules.Catalog.Enums;

namespace Zadana.Application.Modules.Catalog.Queries.GetMasterProducts;

public record GetMasterProductsQuery(
    string? SearchTerm,
    Guid? CategoryId,
    Guid? BrandId,
    ProductStatus? Status = null,
    Guid? VendorId = null,
    int PageNumber = 1,
    int PageSize = 10) : IRequest<PaginatedList<MasterProductDto>>;
