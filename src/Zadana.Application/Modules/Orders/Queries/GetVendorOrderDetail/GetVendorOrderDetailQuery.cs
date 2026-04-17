using MediatR;
using Zadana.Application.Modules.Orders.DTOs;
using Zadana.Application.Modules.Orders.Interfaces;

namespace Zadana.Application.Modules.Orders.Queries.GetVendorOrderDetail;

public record GetVendorOrderDetailQuery(Guid VendorId, Guid OrderId) : IRequest<VendorOrderDetailDto?>;

public class GetVendorOrderDetailQueryHandler : IRequestHandler<GetVendorOrderDetailQuery, VendorOrderDetailDto?>
{
    private readonly IOrderReadService _orderReadService;

    public GetVendorOrderDetailQueryHandler(IOrderReadService orderReadService)
    {
        _orderReadService = orderReadService;
    }

    public Task<VendorOrderDetailDto?> Handle(GetVendorOrderDetailQuery request, CancellationToken cancellationToken) =>
        _orderReadService.GetVendorOrderDetailAsync(request.VendorId, request.OrderId, cancellationToken);
}
