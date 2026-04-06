using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Models;
using Zadana.Application.Modules.Catalog.Interfaces;

namespace Zadana.Application.Modules.Catalog.Queries.ProductRequests.GetVendorRequests;

public class GetVendorProductRequestsQueryHandler : IRequestHandler<GetVendorProductRequestsQuery, PaginatedList<ProductRequestDto>>
{
    private readonly IProductRequestReadService _productRequestReadService;
    private readonly ICurrentVendorService _currentVendorService;

    public GetVendorProductRequestsQueryHandler(
        IProductRequestReadService productRequestReadService,
        ICurrentVendorService currentVendorService)
    {
        _productRequestReadService = productRequestReadService;
        _currentVendorService = currentVendorService;
    }

    public async Task<PaginatedList<ProductRequestDto>> Handle(GetVendorProductRequestsQuery request, CancellationToken cancellationToken)
    {
        var vendorId = await _currentVendorService.TryGetVendorIdAsync(cancellationToken);

        if (vendorId is null)
        {
            return new PaginatedList<ProductRequestDto>([], 0, request.PageNumber, request.PageSize);
        }

        return await _productRequestReadService.GetVendorRequestsAsync(
            vendorId.Value,
            request.Status,
            request.PageNumber,
            request.PageSize,
            cancellationToken);
    }
}
