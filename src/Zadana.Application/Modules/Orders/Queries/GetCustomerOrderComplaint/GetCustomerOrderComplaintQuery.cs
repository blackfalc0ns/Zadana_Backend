using MediatR;
using Zadana.Application.Modules.Orders.DTOs;
using Zadana.Application.Modules.Orders.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Orders.Queries.GetCustomerOrderComplaint;

public record GetCustomerOrderComplaintQuery(Guid OrderId, Guid UserId) : IRequest<OrderComplaintDto>;

public class GetCustomerOrderComplaintQueryHandler : IRequestHandler<GetCustomerOrderComplaintQuery, OrderComplaintDto>
{
    private readonly IOrderReadService _orderReadService;

    public GetCustomerOrderComplaintQueryHandler(IOrderReadService orderReadService)
    {
        _orderReadService = orderReadService;
    }

    public async Task<OrderComplaintDto> Handle(GetCustomerOrderComplaintQuery request, CancellationToken cancellationToken)
    {
        return await _orderReadService.GetCustomerOrderComplaintAsync(request.OrderId, request.UserId, cancellationToken)
            ?? throw new NotFoundException("OrderComplaint", request.OrderId);
    }
}
