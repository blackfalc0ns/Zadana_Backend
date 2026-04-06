using MediatR;
using Zadana.Application.Modules.Orders.DTOs;
using Zadana.Application.Modules.Orders.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Orders.Queries.GetOrderById;

public record GetOrderByIdQuery(Guid Id, Guid UserId) : IRequest<OrderDto>;

public class GetOrderByIdQueryHandler : IRequestHandler<GetOrderByIdQuery, OrderDto>
{
    private readonly IOrderReadService _orderReadService;

    public GetOrderByIdQueryHandler(IOrderReadService orderReadService)
    {
        _orderReadService = orderReadService;
    }

    public async Task<OrderDto> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var order = await _orderReadService.GetByIdAsync(request.Id, request.UserId, cancellationToken);

        if (order == null)
        {
            throw new NotFoundException("Order", request.Id);
        }

        return order;
    }
}
