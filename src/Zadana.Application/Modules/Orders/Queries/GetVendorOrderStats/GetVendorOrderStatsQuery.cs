using MediatR;
using Zadana.Application.Modules.Orders.DTOs;
using Zadana.Application.Modules.Orders.Interfaces;

namespace Zadana.Application.Modules.Orders.Queries.GetVendorOrderStats;

public record GetVendorOrderStatsQuery(Guid VendorId) : IRequest<AdminVendorOrderStatsDto>;

public class GetVendorOrderStatsQueryHandler : IRequestHandler<GetVendorOrderStatsQuery, AdminVendorOrderStatsDto>
{
    private readonly IOrderReadService _orderReadService;

    public GetVendorOrderStatsQueryHandler(IOrderReadService orderReadService)
    {
        _orderReadService = orderReadService;
    }

    public Task<AdminVendorOrderStatsDto> Handle(GetVendorOrderStatsQuery request, CancellationToken cancellationToken) =>
        _orderReadService.GetVendorOrderStatsAsync(request.VendorId, cancellationToken);
}
