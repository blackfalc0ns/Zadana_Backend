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

namespace Zadana.Application.Modules.Orders.Commands.RemoveCartItem;

public record RemoveCartItemCommand(
    CartActor Actor,
    Guid CartItemId,
    Guid? VendorId = null) : IRequest<CartItemRemovalResponseDto>;

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
    private readonly ILogger<RemoveCartItemCommandHandler> _logger;

    public RemoveCartItemCommandHandler(
        IApplicationDbContext context,
        ILogger<RemoveCartItemCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<CartItemRemovalResponseDto> Handle(RemoveCartItemCommand request, CancellationToken cancellationToken)
    {
        var actor = CartActor.Create(request.Actor.UserId, CartLookup.NormalizeGuestId(request.Actor.GuestId));
        try
        {
            return await RemoveAsync(actor, request, cancellationToken);
        }
        catch (Exception exception) when (CartWriteSupport.IsRetryableWriteConflict(exception, actor))
        {
            _logger.LogWarning(
                exception,
                "Retrying RemoveCartItem after cart write conflict for user {UserId} guest {GuestId} item {CartItemId}",
                actor.UserId,
                actor.GuestId,
                request.CartItemId);

            CartWriteSupport.ResetTrackedState(_context);
            return await RemoveAsync(actor, request, cancellationToken);
        }
    }

    private async Task<CartItemRemovalResponseDto> RemoveAsync(
        CartActor actor,
        RemoveCartItemCommand request,
        CancellationToken cancellationToken)
    {
        var cart = await CartLookup.FindCartAsync(_context, actor, cancellationToken, includeItems: true)
            ?? throw new NotFoundException("CartItem", request.CartItemId);

        var cartItem = cart.Items.FirstOrDefault(item => item.Id == request.CartItemId)
            ?? throw new NotFoundException("CartItem", request.CartItemId);

        cart.Items.Remove(cartItem);

        if (cart.Items.Count == 0)
        {
            _context.Carts.Remove(cart);
            await _context.SaveChangesAsync(cancellationToken);

            var emptySummary = new CartSummaryDto(0, 0, null, null, null);
            return new CartItemRemovalResponseDto(
                LocalizedMessages.GetAr(LocalizedMessages.CartItemRemoved),
                LocalizedMessages.GetEn(LocalizedMessages.CartItemRemoved),
                emptySummary);
        }

        await _context.SaveChangesAsync(cancellationToken);

        var refreshedCart = await CartLookup.FindCartAsync(_context, actor, cancellationToken, includeItems: true, asTracking: false);
        if (refreshedCart is null)
        {
            return new CartItemRemovalResponseDto(
                LocalizedMessages.GetAr(LocalizedMessages.CartItemRemoved),
                LocalizedMessages.GetEn(LocalizedMessages.CartItemRemoved),
                new CartSummaryDto(0, 0, null, null, null));
        }

        var effectiveVendorId = request.VendorId
            ?? await CartProjection.ResolveSingleProductPricingVendorIdAsync(_context, refreshedCart, cancellationToken);
        var summary = (await CartProjection.BuildCartDtoAsync(_context, refreshedCart, cancellationToken, effectiveVendorId)).Summary;

        return new CartItemRemovalResponseDto(LocalizedMessages.GetAr(LocalizedMessages.CartItemRemoved), LocalizedMessages.GetEn(LocalizedMessages.CartItemRemoved), summary);
    }
}
