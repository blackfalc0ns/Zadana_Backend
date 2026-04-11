using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Favorites.DTOs;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Favorites.Commands;

public record AddFavoriteCommand(Guid ProductId) : IRequest<AddFavoriteResponse>;

public class AddFavoriteCommandHandler : IRequestHandler<AddFavoriteCommand, AddFavoriteResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public AddFavoriteCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IStringLocalizer<SharedResource> localizer)
    {
        _context = context;
        _currentUserService = currentUserService;
        _localizer = localizer;
    }

    public async Task<AddFavoriteResponse> Handle(AddFavoriteCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            throw new UnauthorizedException(_localizer["UserNotAuthenticated"]);
        }

        var visibleItem = await FavoriteReadModelBuilder.BuildAsync(_context, [request.ProductId], cancellationToken);
        if (!visibleItem.TryGetValue(request.ProductId, out var item))
        {
            throw new NotFoundException("FavoriteProduct", request.ProductId);
        }

        var existing = await _context.CustomerFavorites
            .FirstOrDefaultAsync(x => x.UserId == userId.Value && x.MasterProductId == request.ProductId, cancellationToken);

        if (existing is null)
        {
            _context.CustomerFavorites.Add(new CustomerFavorite(userId.Value, request.ProductId));
            await _context.SaveChangesAsync(cancellationToken);
        }

        var count = await _context.CustomerFavorites.CountAsync(x => x.UserId == userId.Value, cancellationToken);

        return new AddFavoriteResponse(
            "product added to favorites successfully",
            item,
            new FavoritesSummaryDto(count));
    }
}
