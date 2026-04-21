using MediatR;
using Zadana.Application.Common.Models;
using Zadana.Application.Modules.Orders.DTOs;
using Zadana.Application.Modules.Orders.Interfaces;

namespace Zadana.Application.Modules.Orders.Queries.GetVendorOrders;

public record GetVendorOrdersQuery(
    Guid VendorId,
    string? Search = null,
    string? Status = null,
    string? PaymentStatus = null,
    int Page = 1,
    int PageSize = 10) : IRequest<PaginatedList<AdminVendorOrderListItemDto>>;

public class GetVendorOrdersQueryHandler : IRequestHandler<GetVendorOrdersQuery, PaginatedList<AdminVendorOrderListItemDto>>
{
    private readonly IOrderReadService _orderReadService;

    public GetVendorOrdersQueryHandler(IOrderReadService orderReadService)
    {
        _orderReadService = orderReadService;
    }

    public Task<PaginatedList<AdminVendorOrderListItemDto>> Handle(GetVendorOrdersQuery request, CancellationToken cancellationToken) =>
        _orderReadService.GetVendorOrdersAsync(
            request.VendorId,
            request.Search,
            request.Status,
            request.PaymentStatus,
            request.Page,
            request.PageSize,
            cancellationToken);
}
