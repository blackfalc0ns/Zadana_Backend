using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Favorites.DTOs;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Favorites.Commands;

public record RemoveFavoriteCommand(Guid? UserId, string? GuestId, Guid ProductId) : IRequest<RemoveFavoriteResponse>;

public class RemoveFavoriteCommandHandler : IRequestHandler<RemoveFavoriteCommand, RemoveFavoriteResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public RemoveFavoriteCommandHandler(
        IApplicationDbContext context,
        IStringLocalizer<SharedResource> localizer)
    {
        _context = context;
        _localizer = localizer;
    }

    public async Task<RemoveFavoriteResponse> Handle(RemoveFavoriteCommand request, CancellationToken cancellationToken)
    {
        var guestId = string.IsNullOrWhiteSpace(request.GuestId) ? null : request.GuestId.Trim();
        if (!request.UserId.HasValue && guestId is null)
        {
            throw new UnauthorizedException(_localizer["UserNotAuthenticated"]);
        }

        var existing = await _context.CustomerFavorites
            .FirstOrDefaultAsync(x =>
                x.MasterProductId == request.ProductId &&
                ((request.UserId.HasValue && x.UserId == request.UserId.Value) ||
                 (!request.UserId.HasValue && x.GuestId == guestId)),
                cancellationToken);

        if (existing is not null)
        {
            _context.CustomerFavorites.Remove(existing);
            await _context.SaveChangesAsync(cancellationToken);
        }

        var count = await CountFavoritesAsync(request.UserId, guestId, cancellationToken);

        return new RemoveFavoriteResponse(
            _localizer["FavoriteRemovedSuccessfully"],
            new FavoritesSummaryDto(count));
    }

    private Task<int> CountFavoritesAsync(Guid? userId, string? guestId, CancellationToken cancellationToken) =>
        userId.HasValue
            ? _context.CustomerFavorites.CountAsync(x => x.UserId == userId.Value, cancellationToken)
            : _context.CustomerFavorites.CountAsync(x => x.GuestId == guestId, cancellationToken);
}
