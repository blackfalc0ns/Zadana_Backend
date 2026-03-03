using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Orders.Commands.AddToCart;

public class AddToCartCommandHandler : IRequestHandler<AddToCartCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public AddToCartCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(AddToCartCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate VendorProduct exists and has sufficient stock
        var vendorProduct = await _context.VendorProducts
            .FirstOrDefaultAsync(vp => vp.Id == request.VendorProductId, cancellationToken);
            
        if (vendorProduct == null)
            throw new NotFoundException("VendorProduct", request.VendorProductId);

        if (vendorProduct.StockQuantity < request.Quantity)
        {
            throw new BusinessRuleException("INSUFFICIENT_STOCK", "الكمية المطلوبة تتجاوز المخزون المتاح. | Requested quantity exceeds available stock.");
        }
        // 2. Get or Create Cart for User/Vendor
        var cart = await _context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == request.UserId && c.VendorId == request.VendorId, cancellationToken);

        if (cart == null)
            cart = new Cart(request.UserId, request.VendorId);

        // 3. Check if Item already exists in Cart
        var existingItem = cart.Items.FirstOrDefault(i => i.VendorProductId == request.VendorProductId);
        if (existingItem != null)
        {
            existingItem.UpdateQuantity(existingItem.Quantity + request.Quantity);
        }
        else
        {
            var cartItem = new CartItem(
                cart.Id, 
                request.VendorProductId, 
                request.Quantity, 
                vendorProduct.SellingPrice);
                
            cart.Items.Add(cartItem);
        }

        // 4. Update Cart Totals
        var subtotal = cart.Items.Sum(i => i.LineTotal);
        var deliveryFee = 0; // Simplified for now
        cart.UpdateTotals(subtotal, deliveryFee);

        // 5. Save Changes
        if (cart.Id == Guid.Empty)
        {
            _context.Carts.Add(cart);
        }
        else
        {
            _context.Carts.Update(cart);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return cart.Id;
    }
}
