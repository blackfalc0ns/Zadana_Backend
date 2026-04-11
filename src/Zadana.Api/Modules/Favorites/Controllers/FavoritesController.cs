using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Favorites.Requests;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Favorites.Commands;
using Zadana.Application.Modules.Favorites.DTOs;
using Zadana.Application.Modules.Favorites.Queries;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Favorites.Controllers;

[Route("api/favorites")]
[Tags("Customer App API")]
[AllowAnonymous]
public class FavoritesController : ApiControllerBase
{
    private const string GuestDeviceHeader = "X-Device-Id";

    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public FavoritesController(
        IMediator mediator,
        ICurrentUserService currentUserService,
        IStringLocalizer<SharedResource> localizer)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
        _localizer = localizer;
    }

    [HttpGet]
    [ProducesResponseType(typeof(FavoritesListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFavorites(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetFavoritesQuery(_currentUserService.UserId, GetGuestId()), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(AddFavoriteResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddFavorite([FromBody] AddFavoriteRequest request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.ProductId, out var productId))
        {
            throw new BadRequestException("INVALID_PRODUCT_ID", "Invalid product id.");
        }

        var result = await _mediator.Send(new AddFavoriteCommand(_currentUserService.UserId, GetGuestId(), productId), cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{productId}")]
    [ProducesResponseType(typeof(RemoveFavoriteResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveFavorite(string productId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(productId, out var parsedProductId))
        {
            throw new BadRequestException("INVALID_PRODUCT_ID", "Invalid product id.");
        }

        var result = await _mediator.Send(new RemoveFavoriteCommand(_currentUserService.UserId, GetGuestId(), parsedProductId), cancellationToken);
        return Ok(result);
    }

    [HttpDelete]
    [ProducesResponseType(typeof(ClearFavoritesResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ClearFavorites(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ClearFavoritesCommand(_currentUserService.UserId, GetGuestId()), cancellationToken);
        return Ok(result);
    }

    private string? GetGuestId()
    {
        if (_currentUserService.UserId.HasValue)
        {
            return null;
        }

        var guestId = Request.Headers[GuestDeviceHeader].ToString();
        if (!string.IsNullOrWhiteSpace(guestId))
        {
            return guestId.Trim();
        }

        throw new UnauthorizedException($"{_localizer["UserNotAuthenticated"]}. Send {GuestDeviceHeader} for guest favorites access.");
    }
}
