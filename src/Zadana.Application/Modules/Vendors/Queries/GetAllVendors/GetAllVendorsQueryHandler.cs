using MediatR;
using Zadana.Application.Common.Models;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;

namespace Zadana.Application.Modules.Vendors.Queries.GetAllVendors;

public class GetAllVendorsQueryHandler : IRequestHandler<GetAllVendorsQuery, PaginatedList<VendorListItemDto>>
{
    private readonly IVendorReadService _vendorReadService;

    public GetAllVendorsQueryHandler(IVendorReadService vendorReadService)
    {
        _vendorReadService = vendorReadService;
    }

    public Task<PaginatedList<VendorListItemDto>> Handle(GetAllVendorsQuery request, CancellationToken cancellationToken) =>
        _vendorReadService.GetAllAsync(request.Status, request.Search, request.Page, request.PageSize, cancellationToken);
}
