using MediatR;
using Zadana.Application.Modules.Orders.DTOs;
using Zadana.Application.Modules.Orders.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Orders.Queries.GetCustomerOrderTracking;

public record GetCustomerOrderTrackingQuery(Guid OrderId, Guid UserId) : IRequest<CustomerOrderTrackingDto>;

public class GetCustomerOrderTrackingQueryHandler : IRequestHandler<GetCustomerOrderTrackingQuery, CustomerOrderTrackingDto>
{
    private readonly IOrderReadService _orderReadService;

    public GetCustomerOrderTrackingQueryHandler(IOrderReadService orderReadService)
    {
        _orderReadService = orderReadService;
    }

    public async Task<CustomerOrderTrackingDto> Handle(GetCustomerOrderTrackingQuery request, CancellationToken cancellationToken)
    {
        return await _orderReadService.GetCustomerOrderTrackingAsync(request.OrderId, request.UserId, cancellationToken)
            ?? throw new NotFoundException("Order", request.OrderId);
    }
}
