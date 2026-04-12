using MediatR;
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
        var actor = CartActor.Create(request.Actor.UserId, CartLookup.NormalizeGuestId(request.Actor.GuestId));
        var cart = await CartLookup.FindCartAsync(_context, actor, cancellationToken, includeItems: true, asTracking: false);

        return await CartProjection.BuildCartDtoAsync(_context, cart, cancellationToken, request.VendorId);
    }
}
