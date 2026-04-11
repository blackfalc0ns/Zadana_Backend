using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Favorites.DTOs;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Favorites.Commands;

public record ClearFavoritesCommand() : IRequest<ClearFavoritesResponse>;

public class ClearFavoritesCommandHandler : IRequestHandler<ClearFavoritesCommand, ClearFavoritesResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public ClearFavoritesCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IStringLocalizer<SharedResource> localizer)
    {
        _context = context;
        _currentUserService = currentUserService;
        _localizer = localizer;
    }

    public async Task<ClearFavoritesResponse> Handle(ClearFavoritesCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            throw new UnauthorizedException(_localizer["UserNotAuthenticated"]);
        }

        var favorites = await _context.CustomerFavorites
            .Where(x => x.UserId == userId.Value)
            .ToListAsync(cancellationToken);

        if (favorites.Count > 0)
        {
            _context.CustomerFavorites.RemoveRange(favorites);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new ClearFavoritesResponse("favorites cleared successfully");
    }
}
