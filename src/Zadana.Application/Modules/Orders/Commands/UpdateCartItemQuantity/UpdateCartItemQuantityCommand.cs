using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Orders.DTOs;
using Zadana.Application.Modules.Orders.Support;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Orders.Commands.UpdateCartItemQuantity;

public record UpdateCartItemQuantityCommand(
    CartActor Actor,
    Guid CartItemId,
    int Quantity,
    Guid? VendorId = null) : IRequest<CartItemMutationResponseDto>;

public class UpdateCartItemQuantityCommandValidator : AbstractValidator<UpdateCartItemQuantityCommand>
{
    public UpdateCartItemQuantityCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.CartItemId).NotEmpty().WithMessage(x => localizer["RequiredField"]);
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage(x => localizer["GreaterThanZero"]);
    }
}

public class UpdateCartItemQuantityCommandHandler : IRequestHandler<UpdateCartItemQuantityCommand, CartItemMutationResponseDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateCartItemQuantityCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CartItemMutationResponseDto> Handle(UpdateCartItemQuantityCommand request, CancellationToken cancellationToken)
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

        cartItem.UpdateQuantity(request.Quantity);
        cart.UpdateTotals(0, 0);

        await _context.SaveChangesAsync(cancellationToken);

        var cartDto = await CartProjection.BuildCartDtoAsync(_context, cart, cancellationToken, request.VendorId);
        var itemDto = cartDto.Items.Single(item => item.Id == cartItem.Id);

        return new CartItemMutationResponseDto("cart item updated successfully", itemDto, cartDto.Summary);
    }
}
