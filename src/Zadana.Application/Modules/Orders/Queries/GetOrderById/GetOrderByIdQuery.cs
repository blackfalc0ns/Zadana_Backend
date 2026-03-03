using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Orders.DTOs;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Orders.Queries.GetOrderById;

public record GetOrderByIdQuery(Guid Id, Guid UserId) : IRequest<OrderDto>;

public class GetOrderByIdQueryHandler : IRequestHandler<GetOrderByIdQuery, OrderDto>
{
    private readonly IApplicationDbContext _context;

    public GetOrderByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<OrderDto> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == request.Id && o.UserId == request.UserId, cancellationToken);

        if (order == null)
            throw new NotFoundException("Order", request.Id);

        return new OrderDto(
            order.Id,
            order.OrderNumber,
            order.UserId,
            order.VendorId,
            order.CustomerAddressId,
            order.Status.ToString(),
            order.PaymentMethod.ToString(),
            order.PaymentStatus.ToString(),
            order.Subtotal,
            order.DeliveryFee,
            order.TotalAmount,
            order.PlacedAtUtc,
            order.Items.Select(i => new OrderItemDto(
                i.Id,
                i.VendorProductId,
                i.MasterProductId,
                i.ProductName,
                i.Quantity,
                i.UnitPrice,
                i.LineTotal
            )).ToList()
        );
    }
}
