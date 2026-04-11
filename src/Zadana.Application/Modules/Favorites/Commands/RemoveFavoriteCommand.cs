using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Favorites.DTOs;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Favorites.Commands;

public record RemoveFavoriteCommand(Guid ProductId) : IRequest<RemoveFavoriteResponse>;

public class RemoveFavoriteCommandHandler : IRequestHandler<RemoveFavoriteCommand, RemoveFavoriteResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public RemoveFavoriteCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IStringLocalizer<SharedResource> localizer)
    {
        _context = context;
        _currentUserService = currentUserService;
        _localizer = localizer;
    }

    public async Task<RemoveFavoriteResponse> Handle(RemoveFavoriteCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            throw new UnauthorizedException(_localizer["UserNotAuthenticated"]);
        }

        var existing = await _context.CustomerFavorites
            .FirstOrDefaultAsync(x => x.UserId == userId.Value && x.MasterProductId == request.ProductId, cancellationToken);

        if (existing is not null)
        {
            _context.CustomerFavorites.Remove(existing);
            await _context.SaveChangesAsync(cancellationToken);
        }

        var count = await _context.CustomerFavorites.CountAsync(x => x.UserId == userId.Value, cancellationToken);

        return new RemoveFavoriteResponse(
            "product removed from favorites successfully",
            new FavoritesSummaryDto(count));
    }
}
