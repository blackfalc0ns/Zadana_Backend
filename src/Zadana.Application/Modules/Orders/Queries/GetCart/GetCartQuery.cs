using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Orders.DTOs;
using Zadana.Application.Modules.Orders.Support;

namespace Zadana.Application.Modules.Orders.Queries.GetCart;

public record GetCartQuery(CartActor Actor, Guid? VendorId, int Page = 1, int PerPage = 20) : IRequest<CartDto>;

public class GetCartQueryHandler : IRequestHandler<GetCartQuery, CartDto>
{
    private const int DefaultPage = 1;
    private const int DefaultPerPage = 20;
    private const int MaxPerPage = 100;

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
        // returned items list is sliced for the requested page.
        var fullCart = await CartProjection.BuildCartDtoAsync(_context, cart, cancellationToken, request.VendorId, address);

        var page = NormalizePage(request.Page);
        var perPage = NormalizePerPage(request.PerPage);
        var total = fullCart.Items.Count;
        var pagedItems = fullCart.Items
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .ToList();

        return fullCart with
        {
            Items = pagedItems,
            Total = total,
            Page = page,
            PerPage = perPage
        };
    }

    private static int NormalizePage(int page) => page <= 0 ? DefaultPage : page;

    private static int NormalizePerPage(int perPage)
    {
        if (perPage <= 0)
        {
            return DefaultPerPage;
        }

        return Math.Min(perPage, MaxPerPage);
    }
}
