using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Caching;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Favorites.DTOs;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Favorites.Commands;

public record ClearFavoritesCommand(Guid? UserId, string? GuestId) : IRequest<ClearFavoritesResponse>;

public class ClearFavoritesCommandHandler : IRequestHandler<ClearFavoritesCommand, ClearFavoritesResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheInvalidator _cacheInvalidator;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public ClearFavoritesCommandHandler(
        IApplicationDbContext context,
        ICacheInvalidator cacheInvalidator,
        IStringLocalizer<SharedResource> localizer)
    {
        _context = context;
        _cacheInvalidator = cacheInvalidator;
        _localizer = localizer;
    }

    public async Task<ClearFavoritesResponse> Handle(ClearFavoritesCommand request, CancellationToken cancellationToken)
    {
        var guestId = string.IsNullOrWhiteSpace(request.GuestId) ? null : request.GuestId.Trim();
        if (!request.UserId.HasValue && guestId is null)
        {
            throw new UnauthorizedException(_localizer["UserNotAuthenticated"]);
        }

        var favorites = await _context.CustomerFavorites
            .Where(x =>
                (request.UserId.HasValue && x.UserId == request.UserId.Value) ||
                (!request.UserId.HasValue && x.GuestId == guestId))
            .ToListAsync(cancellationToken);

        if (favorites.Count > 0)
        {
            _context.CustomerFavorites.RemoveRange(favorites);
            await _context.SaveChangesAsync(cancellationToken);
            await _cacheInvalidator.RemoveByTagAsync(AppCacheKeys.FavoriteScopeTag(request.UserId, guestId), cancellationToken);
        }

        return new ClearFavoritesResponse(_localizer["FavoritesClearedSuccessfully"]);
    }
}
