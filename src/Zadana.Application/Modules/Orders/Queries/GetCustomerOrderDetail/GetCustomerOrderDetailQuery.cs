using MediatR;
using Zadana.Application.Modules.Orders.DTOs;
using Zadana.Application.Modules.Orders.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Orders.Queries.GetCustomerOrderDetail;

public record GetCustomerOrderDetailQuery(Guid OrderId, Guid UserId) : IRequest<CustomerOrderDetailDto>;

public class GetCustomerOrderDetailQueryHandler : IRequestHandler<GetCustomerOrderDetailQuery, CustomerOrderDetailDto>
{
    private readonly IOrderReadService _orderReadService;

    public GetCustomerOrderDetailQueryHandler(IOrderReadService orderReadService)
    {
        _orderReadService = orderReadService;
    }

    public async Task<CustomerOrderDetailDto> Handle(GetCustomerOrderDetailQuery request, CancellationToken cancellationToken)
    {
        return await _orderReadService.GetCustomerOrderDetailAsync(request.OrderId, request.UserId, cancellationToken)
            ?? throw new NotFoundException("Order", request.OrderId);
    }
}
