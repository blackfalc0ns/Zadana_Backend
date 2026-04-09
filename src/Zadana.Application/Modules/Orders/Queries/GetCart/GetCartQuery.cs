using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Orders.DTOs;
using Zadana.Application.Modules.Orders.Support;

namespace Zadana.Application.Modules.Orders.Queries.GetCart;

public record GetCartQuery(CartActor Actor, Guid? VendorId) : IRequest<CartDto>;

public class GetCartQueryHandler : IRequestHandler<GetCartQuery, CartDto>
{
    private readonly IApplicationDbContext _context;

    public GetCartQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CartDto> Handle(GetCartQuery request, CancellationToken cancellationToken)
    {
        var cart = await _context.Carts
            .Include(item => item.Items)
            .FirstOrDefaultAsync(
                cart => request.Actor.UserId.HasValue
                    ? cart.UserId == request.Actor.UserId.Value
                    : cart.GuestId == request.Actor.GuestId,
                cancellationToken);

        return await CartProjection.BuildCartDtoAsync(_context, cart, cancellationToken, request.VendorId);
    }
}
