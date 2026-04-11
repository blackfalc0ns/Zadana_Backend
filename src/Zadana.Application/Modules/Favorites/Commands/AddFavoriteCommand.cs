using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Favorites.DTOs;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Favorites.Commands;

public record AddFavoriteCommand(Guid? UserId, string? GuestId, Guid ProductId) : IRequest<AddFavoriteResponse>;

public class AddFavoriteCommandHandler : IRequestHandler<AddFavoriteCommand, AddFavoriteResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public AddFavoriteCommandHandler(
        IApplicationDbContext context,
        IStringLocalizer<SharedResource> localizer)
    {
        _context = context;
        _localizer = localizer;
    }

    public async Task<AddFavoriteResponse> Handle(AddFavoriteCommand request, CancellationToken cancellationToken)
    {
        var guestId = string.IsNullOrWhiteSpace(request.GuestId) ? null : request.GuestId.Trim();
        if (!request.UserId.HasValue && guestId is null)
        {
            throw new UnauthorizedException(_localizer["UserNotAuthenticated"]);
        }

        var visibleItem = await FavoriteReadModelBuilder.BuildAsync(_context, [request.ProductId], cancellationToken);
        if (!visibleItem.TryGetValue(request.ProductId, out var item))
        {
            throw new NotFoundException("FavoriteProduct", request.ProductId);
        }

        var existing = await _context.CustomerFavorites
            .FirstOrDefaultAsync(x =>
                x.MasterProductId == request.ProductId &&
                ((request.UserId.HasValue && x.UserId == request.UserId.Value) ||
                 (!request.UserId.HasValue && x.GuestId == guestId)),
                cancellationToken);

        if (existing is null)
        {
            _context.CustomerFavorites.Add(new CustomerFavorite(request.UserId, guestId, request.ProductId));
            await _context.SaveChangesAsync(cancellationToken);
        }

        var count = await CountFavoritesAsync(request.UserId, guestId, cancellationToken);

        return new AddFavoriteResponse(
            "product added to favorites successfully",
            item,
            new FavoritesSummaryDto(count));
    }

    private Task<int> CountFavoritesAsync(Guid? userId, string? guestId, CancellationToken cancellationToken) =>
        userId.HasValue
            ? _context.CustomerFavorites.CountAsync(x => x.UserId == userId.Value, cancellationToken)
            : _context.CustomerFavorites.CountAsync(x => x.GuestId == guestId, cancellationToken);
}
