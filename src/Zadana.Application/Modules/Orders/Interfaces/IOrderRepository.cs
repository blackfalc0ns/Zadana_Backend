using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Payments.Enums;

namespace Zadana.Application.Modules.Orders.Interfaces;

public interface IOrderRepository
{
    Task<MasterProduct?> GetMasterProductAsync(Guid masterProductId, CancellationToken cancellationToken = default);
    Task<Cart?> GetCartAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Cart?> GetCartForCheckoutAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, VendorProduct>> GetVendorProductsForCheckoutAsync(
        Guid vendorId,
        IReadOnlyCollection<Guid> masterProductIds,
        CancellationToken cancellationToken = default);
    Task<Order?> GetReusablePendingOrderForCheckoutAsync(
        Guid userId,
        Guid vendorId,
        Guid customerAddressId,
        PaymentMethodType paymentMethod,
        Guid? vendorBranchId,
        Guid? couponId,
        string? notes,
        decimal subtotal,
        decimal discountTotal,
        decimal deliveryFee,
        decimal baseDeliveryFee,
        decimal distanceDeliveryFee,
        decimal surgeDeliveryFee,
        decimal? quotedDistanceKm,
        string? deliveryPricingMode,
        string? deliveryPricingRuleLabel,
        decimal commissionAmount,
        IReadOnlyDictionary<Guid, int> itemQuantities,
        CancellationToken cancellationToken = default);
    void AddCart(Cart cart);
    void UpdateCart(Cart cart);
    void RemoveCart(Cart cart);
    void AddOrder(Order order);
    void AddOrderItem(OrderItem orderItem);
}
