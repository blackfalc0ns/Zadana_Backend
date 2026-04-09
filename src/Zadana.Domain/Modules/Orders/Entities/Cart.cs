using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Marketing.Entities;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Orders.Entities;

public class Cart : BaseEntity
{
    public Guid? UserId { get; private set; }
    public string? GuestId { get; private set; }
    public Guid? CouponId { get; private set; }
    
    public decimal Subtotal { get; private set; }
    public decimal DiscountTotal { get; private set; }
    public decimal DeliveryFee { get; private set; }
    public decimal Total { get; private set; }

    // Navigation
    public User? User { get; private set; }
    // Note: Coupon is resolved from Marketing module
    
    public ICollection<CartItem> Items { get; private set; } = [];

    private Cart() { }

    public Cart(Guid? userId, string? guestId = null)
    {
        if (!userId.HasValue && string.IsNullOrWhiteSpace(guestId))
        {
            throw new InvalidOperationException("Cart owner is required.");
        }

        UserId = userId;
        GuestId = guestId?.Trim();
        Subtotal = 0;
        DiscountTotal = 0;
        DeliveryFee = 0;
        Total = 0;
    }

    public void ApplyCoupon(Guid couponId, decimal discountAmount)
    {
        CouponId = couponId;
        DiscountTotal = discountAmount;
        RecalculateTotal();
    }

    public void RemoveCoupon()
    {
        CouponId = null;
        DiscountTotal = 0;
        RecalculateTotal();
    }

    public void UpdateTotals(decimal subtotal, decimal deliveryFee)
    {
        Subtotal = subtotal;
        DeliveryFee = deliveryFee;
        RecalculateTotal();
    }

    private void RecalculateTotal()
    {
        Total = Math.Max(0, Subtotal - DiscountTotal + DeliveryFee);
    }
}
