using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Orders.DTOs;
using Zadana.Application.Modules.Orders.Support;

namespace Zadana.Application.Modules.Orders.Commands.ClearCart;

public record ClearCartCommand(CartActor Actor) : IRequest<CartClearResponseDto>;

public class ClearCartCommandValidator : AbstractValidator<ClearCartCommand>
{
    public ClearCartCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.Actor).NotNull().WithMessage(x => localizer["RequiredField"]);
    }
}

public class ClearCartCommandHandler : IRequestHandler<ClearCartCommand, CartClearResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<ClearCartCommandHandler> _logger;

    public ClearCartCommandHandler(
        IApplicationDbContext context,
        ILogger<ClearCartCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<CartClearResponseDto> Handle(ClearCartCommand request, CancellationToken cancellationToken)
    {
        var actor = CartActor.Create(request.Actor.UserId, CartLookup.NormalizeGuestId(request.Actor.GuestId));
        try
        {
            return await ClearAsync(actor, cancellationToken);
        }
        catch (Exception exception) when (CartWriteSupport.IsRetryableWriteConflict(exception, actor))
        {
            _logger.LogWarning(
                exception,
                "Retrying ClearCart after cart write conflict for user {UserId} guest {GuestId}",
                actor.UserId,
                actor.GuestId);

            CartWriteSupport.ResetTrackedState(_context);
            return await ClearAsync(actor, cancellationToken);
        }
    }

    private async Task<CartClearResponseDto> ClearAsync(CartActor actor, CancellationToken cancellationToken)
    {
        var cart = await CartLookup.FindCartAsync(_context, actor, cancellationToken, includeItems: true);
        if (cart is not null)
        {
            _context.Carts.Remove(cart);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new CartClearResponseDto("cart cleared successfully");
    }
}
