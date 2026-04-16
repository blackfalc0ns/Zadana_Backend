using MediatR;
using Zadana.Application.Modules.Orders.DTOs;
using Zadana.Application.Modules.Orders.Interfaces;

namespace Zadana.Application.Modules.Orders.Queries.GetCustomerOrders;

public record GetCustomerOrdersQuery(
    Guid UserId,
    CustomerOrderBucket Bucket,
    int Page = 1,
    int PerPage = 20) : IRequest<CustomerOrderListDto>;

public class GetCustomerOrdersQueryHandler : IRequestHandler<GetCustomerOrdersQuery, CustomerOrderListDto>
{
    private readonly IOrderReadService _orderReadService;

    public GetCustomerOrdersQueryHandler(IOrderReadService orderReadService)
    {
        _orderReadService = orderReadService;
    }

    public Task<CustomerOrderListDto> Handle(GetCustomerOrdersQuery request, CancellationToken cancellationToken) =>
        _orderReadService.GetCustomerOrdersAsync(request.UserId, request.Bucket, request.Page, request.PerPage, cancellationToken);
}
