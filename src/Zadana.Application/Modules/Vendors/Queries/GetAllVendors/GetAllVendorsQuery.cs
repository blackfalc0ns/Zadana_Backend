using MediatR;
using Zadana.Application.Common.Models;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Domain.Modules.Vendors.Enums;

namespace Zadana.Application.Modules.Vendors.Queries.GetAllVendors;

public record GetAllVendorsQuery(
    VendorStatus? Status,
    string? Search,
    int Page = 1,
    int PageSize = 10) : IRequest<PaginatedList<VendorListItemDto>>;
