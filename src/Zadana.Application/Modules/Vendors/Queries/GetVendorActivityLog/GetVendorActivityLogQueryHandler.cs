using MediatR;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Queries.GetVendorActivityLog;

public class GetVendorActivityLogQueryHandler : IRequestHandler<GetVendorActivityLogQuery, VendorActivityLogPageDto>
{
    private readonly IVendorReadService _vendorReadService;

    public GetVendorActivityLogQueryHandler(IVendorReadService vendorReadService)
    {
        _vendorReadService = vendorReadService;
    }

    public async Task<VendorActivityLogPageDto> Handle(GetVendorActivityLogQuery request, CancellationToken cancellationToken) =>
        await _vendorReadService.GetActivityLogAsync(
            request.VendorId,
            request.Type,
            request.Severity,
            request.DateFrom,
            request.DateTo,
            request.Page,
            request.PageSize,
            cancellationToken)
        ?? throw new NotFoundException("Vendor", request.VendorId);
}
