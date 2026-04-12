using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Orders.DTOs;
using Zadana.Application.Modules.Orders.Support;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Orders.Commands.AddCartItem;

public record AddCartItemCommand(
    CartActor Actor,
    Guid ProductId,
    int Quantity) : IRequest<CartItemMutationResponseDto>;

public class AddCartItemCommandValidator : AbstractValidator<AddCartItemCommand>
{
    public AddCartItemCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage(x => localizer["RequiredField"]);
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage(x => localizer["GreaterThanZero"]);
    }
}

public class AddCartItemCommandHandler : IRequestHandler<AddCartItemCommand, CartItemMutationResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<AddCartItemCommandHandler> _logger;

    public AddCartItemCommandHandler(
        IApplicationDbContext context,
        ILogger<AddCartItemCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<CartItemMutationResponseDto> Handle(AddCartItemCommand request, CancellationToken cancellationToken)
    {
        var actor = CartActor.Create(request.Actor.UserId, CartLookup.NormalizeGuestId(request.Actor.GuestId));
        var product = await _context.MasterProducts
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Id == request.ProductId && item.Status == ProductStatus.Active,
                cancellationToken);

        if (product is null)
        {
            throw new NotFoundException("MasterProduct", request.ProductId);
        }

        Cart? cart = null;
        CartItem? affectedItem = null;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                cart = await EnsureCartForWriteAsync(actor, cancellationToken);
                affectedItem = await AddOrUpdateItemAsync(cart, product, request, cancellationToken);
                break;
            }
            catch (Exception exception) when (attempt < 2 && CartWriteSupport.IsRetryableWriteConflict(exception, actor))
            {
                _logger.LogWarning(
                    exception,
                    "Retrying AddCartItem after cart write conflict for user {UserId} guest {GuestId} product {ProductId}. Attempt {Attempt}",
                    actor.UserId,
                    actor.GuestId,
                    request.ProductId,
                    attempt + 1);

                CartWriteSupport.ResetTrackedState(_context);
            }
        }

        if (cart is null || affectedItem is null)
        {
            throw new InvalidOperationException("Cart item could not be added.");
        }

        cart = await ReloadCartForWriteAsync(actor, cancellationToken);
        var cartDto = await CartProjection.BuildCartDtoAsync(_context, cart, cancellationToken, null);
        var itemDto = cartDto.Items.Single(item => item.Id == affectedItem.Id);

        return new CartItemMutationResponseDto("added to cart successfully", itemDto, cartDto.Summary);
    }

    private async Task<Cart> EnsureCartForWriteAsync(CartActor actor, CancellationToken cancellationToken)
    {
        var cart = await CartLookup.FindCartAsync(_context, actor, cancellationToken, includeItems: true);
        if (cart is not null)
        {
            return cart;
        }

        if (_context is DbContext dbContext
            && string.Equals(dbContext.Database.ProviderName, "Microsoft.EntityFrameworkCore.InMemory", StringComparison.Ordinal))
        {
            cart = new Cart(actor.UserId, actor.GuestId);
            _context.Carts.Add(cart);
            return cart;
        }

        cart = new Cart(actor.UserId, actor.GuestId);
        _context.Carts.Add(cart);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return cart;
        }
        catch (Exception exception) when (CartWriteSupport.IsRetryableWriteConflict(exception, actor))
        {
            _logger.LogWarning(
                exception,
                "Retrying cart creation after conflict for user {UserId} guest {GuestId}",
                actor.UserId,
                actor.GuestId);

            CartWriteSupport.ResetTrackedState(_context);
            return await ReloadCartForWriteAsync(actor, cancellationToken);
        }
    }

    private async Task<CartItem> AddOrUpdateItemAsync(
        Cart cart,
        MasterProduct product,
        AddCartItemCommand request,
        CancellationToken cancellationToken)
    {
        var existingItem = cart.Items.FirstOrDefault(item => item.MasterProductId == request.ProductId);
        CartItem affectedItem;

        if (existingItem is not null)
        {
            existingItem.UpdateQuantity(existingItem.Quantity + request.Quantity);
            affectedItem = existingItem;
        }
        else
        {
            affectedItem = new CartItem(
                cart.Id,
                request.ProductId,
                !string.IsNullOrWhiteSpace(product.NameEn) ? product.NameEn : product.NameAr,
                request.Quantity);

            _context.CartItems.Add(affectedItem);
        }

        if (_context is DbContext dbContext && dbContext.Entry(cart).State != EntityState.Added)
        {
            dbContext.Entry(cart).State = EntityState.Unchanged;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return affectedItem;
    }

    private async Task<Cart> ReloadCartForWriteAsync(CartActor actor, CancellationToken cancellationToken)
    {
        var cart = await CartLookup.FindCartAsync(_context, actor, cancellationToken, includeItems: true);
        if (cart is null)
        {
            throw new NotFoundException("Cart", actor.UserId ?? Guid.Empty);
        }

        return cart;
    }
}
