using MediatR;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Orders.Interfaces;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Orders.Commands.PlaceOrder;

public class PlaceOrderCommandHandler : IRequestHandler<PlaceOrderCommand, Guid>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly IUnitOfWork _unitOfWork;

    public PlaceOrderCommandHandler(
        IOrderRepository orderRepository,
        IStringLocalizer<SharedResource> localizer,
        IUnitOfWork unitOfWork)
    {
        _orderRepository = orderRepository;
        _localizer = localizer;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(PlaceOrderCommand request, CancellationToken cancellationToken)
    {
        var cart = await _orderRepository.GetCartForCheckoutAsync(request.UserId, cancellationToken);

        if (cart == null || !cart.Items.Any())
        {
            throw new BusinessRuleException("EMPTY_CART", _localizer["EMPTY_CART"]);
        }

        if (!Enum.TryParse<PaymentMethodType>(request.PaymentMethod, true, out var paymentMethod))
        {
            throw new BusinessRuleException("INVALID_PAYMENT", _localizer["INVALID_PAYMENT"]);
        }

        var masterProductIds = cart.Items.Select(item => item.MasterProductId).Distinct().ToArray();
        var vendorProducts = await _orderRepository.GetVendorProductsForCheckoutAsync(
            request.VendorId,
            masterProductIds,
            cancellationToken);

        foreach (var cartItem in cart.Items)
        {
            if (!vendorProducts.TryGetValue(cartItem.MasterProductId, out var vendorProduct))
            {
                throw new BusinessRuleException("VENDOR_MISSING_CART_PRODUCT", _localizer["VENDOR_MISSING_CART_PRODUCT"]);
            }

            if (vendorProduct.StockQuantity < cartItem.Quantity)
            {
                throw new BusinessRuleException("INSUFFICIENT_STOCK", _localizer["INSUFFICIENT_STOCK"]);
            }
        }

        var subtotal = cart.Items.Sum(item => vendorProducts[item.MasterProductId].SellingPrice * item.Quantity);
        var orderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";
        var commissionAmount = subtotal * 0.05m;

        var order = new Order(
            orderNumber: orderNumber,
            userId: request.UserId,
            vendorId: request.VendorId,
            customerAddressId: request.CustomerAddressId,
            paymentMethod: paymentMethod,
            subtotal: subtotal,
            discountTotal: cart.DiscountTotal,
            deliveryFee: cart.DeliveryFee,
            commissionAmount: commissionAmount,
            notes: request.Notes,
            vendorBranchId: request.VendorBranchId,
            couponId: request.CouponId
        );

        _orderRepository.AddOrder(order);

        foreach (var item in cart.Items)
        {
            var vendorProduct = vendorProducts[item.MasterProductId];
            var orderItem = new OrderItem(
                orderId: order.Id,
                vendorProductId: vendorProduct.Id,
                masterProductId: item.MasterProductId,
                productName: item.ProductName,
                quantity: item.Quantity,
                unitPrice: vendorProduct.SellingPrice
            );

            _orderRepository.AddOrderItem(orderItem);
        }

        if (request.ClearCartAfterPlacement)
        {
            _orderRepository.RemoveCart(cart);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return order.Id;
    }
}
