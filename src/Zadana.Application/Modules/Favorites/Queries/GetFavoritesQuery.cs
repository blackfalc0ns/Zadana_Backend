using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Favorites.DTOs;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Favorites.Queries;

public record GetFavoritesQuery() : IRequest<FavoritesListResponse>;

public class GetFavoritesQueryHandler : IRequestHandler<GetFavoritesQuery, FavoritesListResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public GetFavoritesQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IStringLocalizer<SharedResource> localizer)
    {
        _context = context;
        _currentUserService = currentUserService;
        _localizer = localizer;
    }

    public async Task<FavoritesListResponse> Handle(GetFavoritesQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            throw new UnauthorizedException(_localizer["UserNotAuthenticated"]);
        }

        var favoriteIds = await _context.CustomerFavorites
            .AsNoTracking()
            .Where(x => x.UserId == userId.Value)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => x.MasterProductId)
            .ToListAsync(cancellationToken);

        var itemMap = await FavoriteReadModelBuilder.BuildAsync(_context, favoriteIds, cancellationToken);
        var items = favoriteIds
            .Distinct()
            .Where(itemMap.ContainsKey)
            .Select(id => itemMap[id])
            .ToList();

        return new FavoritesListResponse(
            items,
            new FavoritesSummaryDto(items.Count));
    }
}
