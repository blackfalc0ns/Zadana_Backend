using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Models;
using Zadana.Application.Modules.Orders.DTOs;
using Zadana.Application.Modules.Orders.Support;

namespace Zadana.Application.Modules.Orders.Queries.GetCart;

public record GetCartQuery(CartActor Actor, Guid? VendorId, int Limit = 20, int Offset = 0) : IRequest<CartDto>;

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
        if (actor.UserId.HasValue)
        {
            await CartCleanupSupport.ClearStalePaidCheckoutCartIfNeededAsync(
                _context,
                actor.UserId.Value,
                actor.GuestId,
                cancellationToken);
        }

        var cart = await CartLookup.FindCartAsync(_context, actor, cancellationToken, includeItems: true, asTracking: false);
        var address = await CartBranchSelectionSupport.ResolveDefaultAddressAsync(_context, actor, cancellationToken);

        // The full cart is projected first so the summary (pricing totals, checkout
        // eligibility, unavailable counts) always reflects every item, while only the
        // returned items list is sliced for the requested offset/limit.
        var fullCart = await CartProjection.BuildCartDtoAsync(_context, cart, cancellationToken, request.VendorId, address);

        var offset = OffsetLimitPagination.NormalizeOffset(request.Offset);
        var limit = OffsetLimitPagination.NormalizeLimit(request.Limit);
        var total = fullCart.Items.Count;
        var slicedItems = fullCart.Items
            .Skip(offset)
            .Take(limit)
            .ToList();

        return fullCart with
        {
            Items = slicedItems,
            Total = total,
            Limit = limit,
            Offset = offset,
            HasMore = OffsetLimitPagination.HasMore(offset, limit, total)
        };
    }
}
