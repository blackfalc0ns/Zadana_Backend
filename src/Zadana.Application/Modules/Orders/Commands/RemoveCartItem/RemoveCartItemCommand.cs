using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Orders.DTOs;
using Zadana.Application.Modules.Orders.Support;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Orders.Commands.RemoveCartItem;

public record RemoveCartItemCommand(
    CartActor Actor,
    Guid CartItemId) : IRequest<CartItemRemovalResponseDto>;

public class RemoveCartItemCommandValidator : AbstractValidator<RemoveCartItemCommand>
{
    public RemoveCartItemCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.CartItemId).NotEmpty().WithMessage(x => localizer["RequiredField"]);
    }
}

public class RemoveCartItemCommandHandler : IRequestHandler<RemoveCartItemCommand, CartItemRemovalResponseDto>
{
    private readonly IApplicationDbContext _context;

    public RemoveCartItemCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CartItemRemovalResponseDto> Handle(RemoveCartItemCommand request, CancellationToken cancellationToken)
    {
        var cart = await _context.Carts
            .Include(item => item.Items)
            .FirstOrDefaultAsync(
                item => request.Actor.UserId.HasValue
                    ? item.UserId == request.Actor.UserId.Value
                    : item.GuestId == request.Actor.GuestId,
                cancellationToken)
            ?? throw new NotFoundException("CartItem", request.CartItemId);

        var cartItem = cart.Items.FirstOrDefault(item => item.Id == request.CartItemId)
            ?? throw new NotFoundException("CartItem", request.CartItemId);

        cart.Items.Remove(cartItem);

        CartSummaryDto summary;
        if (cart.Items.Count == 0)
        {
            _context.Carts.Remove(cart);
            summary = new CartSummaryDto(0, 0, null, null, null);
        }
        else
        {
            cart.UpdateTotals(0, 0);
            var cartDto = await CartProjection.BuildCartDtoAsync(_context, cart, cancellationToken);
            summary = cartDto.Summary;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new CartItemRemovalResponseDto("cart item removed successfully", summary);
    }
}
