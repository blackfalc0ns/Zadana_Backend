using MediatR;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;

namespace Zadana.Application.Modules.Vendors.Queries.GetAdminVendorStats;

public record GetAdminVendorStatsQuery : IRequest<AdminVendorStatsDto>;

public class GetAdminVendorStatsQueryHandler : IRequestHandler<GetAdminVendorStatsQuery, AdminVendorStatsDto>
{
    private readonly IVendorReadService _vendorReadService;

    public GetAdminVendorStatsQueryHandler(IVendorReadService vendorReadService)
    {
        _vendorReadService = vendorReadService;
    }

    public Task<AdminVendorStatsDto> Handle(GetAdminVendorStatsQuery request, CancellationToken cancellationToken) =>
        _vendorReadService.GetStatsAsync(cancellationToken);
}
