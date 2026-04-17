using MediatR;
using Zadana.Application.Common.Models;
using Zadana.Application.Modules.Orders.DTOs;
using Zadana.Application.Modules.Orders.Interfaces;

namespace Zadana.Application.Modules.Orders.Queries.GetVendorWorkspaceOrders;

public record GetVendorWorkspaceOrdersQuery(
    Guid VendorId,
    string? Search,
    string? Status,
    string? PaymentMethod,
    int Page = 1,
    int PageSize = 10) : IRequest<PaginatedList<VendorOrderListItemDto>>;

public class GetVendorWorkspaceOrdersQueryHandler : IRequestHandler<GetVendorWorkspaceOrdersQuery, PaginatedList<VendorOrderListItemDto>>
{
    private readonly IOrderReadService _orderReadService;

    public GetVendorWorkspaceOrdersQueryHandler(IOrderReadService orderReadService)
    {
        _orderReadService = orderReadService;
    }

    public Task<PaginatedList<VendorOrderListItemDto>> Handle(GetVendorWorkspaceOrdersQuery request, CancellationToken cancellationToken) =>
        _orderReadService.GetVendorWorkspaceOrdersAsync(
            request.VendorId,
            request.Search,
            request.Status,
            request.PaymentMethod,
            request.Page,
            request.PageSize,
            cancellationToken);
}
