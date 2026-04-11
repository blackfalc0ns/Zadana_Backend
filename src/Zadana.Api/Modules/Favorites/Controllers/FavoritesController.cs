using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Favorites.Requests;
using Zadana.Application.Modules.Favorites.Commands;
using Zadana.Application.Modules.Favorites.DTOs;
using Zadana.Application.Modules.Favorites.Queries;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Favorites.Controllers;

[Route("api/favorites")]
[Tags("Customer App API")]
[Authorize(Policy = "CustomerOnly")]
public class FavoritesController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public FavoritesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [ProducesResponseType(typeof(FavoritesListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFavorites(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetFavoritesQuery(), cancellationToken);
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

        var result = await _mediator.Send(new AddFavoriteCommand(productId), cancellationToken);
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

        var result = await _mediator.Send(new RemoveFavoriteCommand(parsedProductId), cancellationToken);
        return Ok(result);
    }

    [HttpDelete]
    [ProducesResponseType(typeof(ClearFavoritesResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ClearFavorites(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ClearFavoritesCommand(), cancellationToken);
        return Ok(result);
    }
}
