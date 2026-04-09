using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Orders.DTOs;
using Zadana.Application.Modules.Orders.Support;
using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Orders.Commands.AddCartItem;

public record AddCartItemCommand(
    CartActor Actor,
    Guid ProductId,
    int Quantity,
    Guid? VendorId) : IRequest<CartItemMutationResponseDto>;

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

    public AddCartItemCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CartItemMutationResponseDto> Handle(AddCartItemCommand request, CancellationToken cancellationToken)
    {
        var product = await _context.MasterProducts
            .FirstOrDefaultAsync(
                item => item.Id == request.ProductId && item.Status == ProductStatus.Active,
                cancellationToken);

        if (product is null)
        {
            throw new NotFoundException("MasterProduct", request.ProductId);
        }

        if (!await CartProjection.HasVisibleOfferAsync(_context, request.ProductId, request.VendorId, cancellationToken))
        {
            throw new BusinessRuleException("PRODUCT_NOT_AVAILABLE", "Product is not available.");
        }

        var cart = await _context.Carts
            .Include(item => item.Items)
            .FirstOrDefaultAsync(
                item => request.Actor.UserId.HasValue
                    ? item.UserId == request.Actor.UserId.Value
                    : item.GuestId == request.Actor.GuestId,
                cancellationToken);

        if (cart is null)
        {
            cart = new Cart(request.Actor.UserId, request.Actor.GuestId);
            _context.Carts.Add(cart);
        }

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

            cart.Items.Add(affectedItem);
        }

        cart.UpdateTotals(0, 0);
        await _context.SaveChangesAsync(cancellationToken);

        var cartDto = await CartProjection.BuildCartDtoAsync(_context, cart, cancellationToken, request.VendorId);
        var itemDto = cartDto.Items.Single(item => item.Id == affectedItem.Id);

        return new CartItemMutationResponseDto("added to cart successfully", itemDto, cartDto.Summary);
    }
}
