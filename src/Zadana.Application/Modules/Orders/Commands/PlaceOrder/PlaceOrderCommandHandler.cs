using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Orders.Commands.PlaceOrder;

public class PlaceOrderCommandHandler : IRequestHandler<PlaceOrderCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public PlaceOrderCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(PlaceOrderCommand request, CancellationToken cancellationToken)
    {
        // 1. Get User's Cart for the Vendor
        var cart = await _context.Carts
            .Include(c => c.Items)
                .ThenInclude(i => i.VendorProduct)
                    .ThenInclude(vp => vp.MasterProduct)
            .FirstOrDefaultAsync(c => c.UserId == request.UserId && c.VendorId == request.VendorId, cancellationToken);

        if (cart == null || !cart.Items.Any())
            throw new BusinessRuleException("EMPTY_CART", "لا يمكنك إنشاء طلب بسلة فارغة. | Cannot place an order with an empty cart.");

        // 2. Map Payment Method
        if (!Enum.TryParse<PaymentMethodType>(request.PaymentMethod, true, out var paymentMethod))
        {
            throw new BusinessRuleException("INVALID_PAYMENT", "طريقة الدفع غير صالحة. | Invalid payment method.");
        }

        // 3. Generate Order Number
        var orderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";

        // 4. Calculate Commission (mocked at 5% for now)
        var commissionAmount = cart.Subtotal * 0.05m;

        // 5. Create Order
        var order = new Order(
            orderNumber: orderNumber,
            userId: request.UserId,
            vendorId: request.VendorId,
            customerAddressId: request.CustomerAddressId,
            paymentMethod: paymentMethod,
            subtotal: cart.Subtotal,
            discountTotal: cart.DiscountTotal,
            deliveryFee: cart.DeliveryFee,
            commissionAmount: commissionAmount,
            notes: request.Notes,
            vendorBranchId: request.VendorBranchId,
            couponId: request.CouponId
        );

        _context.Orders.Add(order);

        // 6. Create Order Items
        foreach (var item in cart.Items)
        {
            var orderItem = new OrderItem(
                orderId: order.Id,
                vendorProductId: item.VendorProductId,
                masterProductId: item.VendorProduct.MasterProduct.Id,
                productName: item.VendorProduct.MasterProduct.NameAr, // Using Ar as default
                quantity: item.Quantity,
                unitPrice: item.UnitPrice
            );
            _context.OrderItems.Add(orderItem);
        }

        // 7. Clear Cart
        _context.Carts.Remove(cart); // Or just empty the items

        await _context.SaveChangesAsync(cancellationToken);

        return order.Id;
    }
}
