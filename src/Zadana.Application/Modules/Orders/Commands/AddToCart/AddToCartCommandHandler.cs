using MediatR;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Orders.Interfaces;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Orders.Commands.AddToCart;

public class AddToCartCommandHandler : IRequestHandler<AddToCartCommand, Guid>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly IUnitOfWork _unitOfWork;

    public AddToCartCommandHandler(
        IOrderRepository orderRepository,
        IStringLocalizer<SharedResource> localizer,
        IUnitOfWork unitOfWork)
    {
        _orderRepository = orderRepository;
        _localizer = localizer;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(AddToCartCommand request, CancellationToken cancellationToken)
    {
        var masterProduct = await _orderRepository.GetMasterProductAsync(request.MasterProductId, cancellationToken);

        if (masterProduct == null)
        {
            throw new NotFoundException("MasterProduct", request.MasterProductId);
        }

        var cart = await _orderRepository.GetCartAsync(request.UserId, cancellationToken);
        var isNewCart = cart == null;

        if (cart == null)
        {
            cart = new Cart(request.UserId);
        }

        var existingItem = cart.Items.FirstOrDefault(item => item.MasterProductId == request.MasterProductId);
        if (existingItem != null)
        {
            existingItem.UpdateQuantity(existingItem.Quantity + request.Quantity);
        }
        else
        {
            var cartItem = new CartItem(
                cart.Id,
                request.MasterProductId,
                !string.IsNullOrWhiteSpace(masterProduct.NameEn) ? masterProduct.NameEn : masterProduct.NameAr,
                request.Quantity);

            cart.Items.Add(cartItem);
        }

        cart.UpdateTotals(0, 0);

        if (isNewCart)
        {
            _orderRepository.AddCart(cart);
        }
        else
        {
            _orderRepository.UpdateCart(cart);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return cart.Id;
    }
}
