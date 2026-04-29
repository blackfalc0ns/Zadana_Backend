using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<UpdateCartItemQuantityCommandHandler> _logger;

    public UpdateCartItemQuantityCommandHandler(
        IApplicationDbContext context,
        ILogger<UpdateCartItemQuantityCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<CartItemMutationResponseDto> Handle(UpdateCartItemQuantityCommand request, CancellationToken cancellationToken)
    {
        var actor = CartActor.Create(request.Actor.UserId, CartLookup.NormalizeGuestId(request.Actor.GuestId));

        try
        {
            return await UpdateAsync(actor, request, cancellationToken);
        }
        catch (Exception exception) when (CartWriteSupport.IsRetryableWriteConflict(exception, actor))
        {
            _logger.LogWarning(
                exception,
                "Retrying UpdateCartItemQuantity after cart write conflict for user {UserId} guest {GuestId} item {CartItemId}",
                actor.UserId,
                actor.GuestId,
                request.CartItemId);

            CartWriteSupport.ResetTrackedState(_context);
            return await UpdateAsync(actor, request, cancellationToken);
        }
    }

    private async Task<CartItemMutationResponseDto> UpdateAsync(
        CartActor actor,
        UpdateCartItemQuantityCommand request,
        CancellationToken cancellationToken)
    {
        var cart = await CartLookup.FindCartAsync(_context, actor, cancellationToken, includeItems: true)
            ?? throw new NotFoundException("CartItem", request.CartItemId);

        var cartItem = cart.Items.FirstOrDefault(item => item.Id == request.CartItemId)
            ?? throw new NotFoundException("CartItem", request.CartItemId);

        cartItem.UpdateQuantity(request.Quantity);

        await _context.SaveChangesAsync(cancellationToken);

        var cartDto = await CartProjection.BuildCartDtoAsync(_context, cart, cancellationToken, request.VendorId);
        var itemDto = cartDto.Items.Single(item => item.Id == cartItem.Id);

        return new CartItemMutationResponseDto(LocalizedMessages.GetAr(LocalizedMessages.CartItemUpdated), LocalizedMessages.GetEn(LocalizedMessages.CartItemUpdated), itemDto, cartDto.Summary);
    }
}
