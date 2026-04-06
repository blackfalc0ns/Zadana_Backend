using MediatR;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Queries.GetVendorDetail;

public class GetVendorDetailQueryHandler : IRequestHandler<GetVendorDetailQuery, VendorDetailDto>
{
    private readonly IVendorReadService _vendorReadService;

    public GetVendorDetailQueryHandler(IVendorReadService vendorReadService)
    {
        _vendorReadService = vendorReadService;
    }

    public async Task<VendorDetailDto> Handle(GetVendorDetailQuery request, CancellationToken cancellationToken) =>
        await _vendorReadService.GetDetailAsync(request.VendorId, cancellationToken)
        ?? throw new NotFoundException("Vendor", request.VendorId);
}
