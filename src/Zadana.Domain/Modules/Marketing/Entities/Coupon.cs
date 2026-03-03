using Zadana.Domain.Modules.Marketing.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Marketing.Entities;

public class Coupon : BaseEntity
{
    public string Code { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public CouponDiscountType DiscountType { get; private set; }
    public decimal DiscountValue { get; private set; }
    
    public decimal? MinOrderAmount { get; private set; }
    public decimal? MaxDiscountAmount { get; private set; }
    
    public DateTime? StartsAtUtc { get; private set; }
    public DateTime? EndsAtUtc { get; private set; }
    
    public int? UsageLimit { get; private set; }
    public int? PerUserLimit { get; private set; }
    
    public bool IsActive { get; private set; }

    // Navigation
    public ICollection<CouponVendor> ApplicableVendors { get; private set; } = [];

    private Coupon() { }

    public Coupon(
        string code,
        string title,
        CouponDiscountType discountType,
        decimal discountValue,
        decimal? minOrderAmount = null,
        decimal? maxDiscountAmount = null,
        DateTime? startsAtUtc = null,
        DateTime? endsAtUtc = null,
        int? usageLimit = null,
        int? perUserLimit = null)
    {
        if (discountValue <= 0) throw new BusinessRuleException("INVALID_DISCOUNT", "Discount value must be greater than zero.");
        if (discountType == CouponDiscountType.Percentage && discountValue > 100) 
            throw new BusinessRuleException("INVALID_DISCOUNT", "Percentage discount cannot exceed 100.");

        Code = code.Trim().ToUpperInvariant();
        Title = title.Trim();
        DiscountType = discountType;
        DiscountValue = discountValue;
        MinOrderAmount = minOrderAmount;
        MaxDiscountAmount = maxDiscountAmount;
        StartsAtUtc = startsAtUtc;
        EndsAtUtc = endsAtUtc;
        UsageLimit = usageLimit;
        PerUserLimit = perUserLimit;
        IsActive = true;
    }

    public void UpdateStatus(bool isActive) => IsActive = isActive;
    
    public bool IsValid()
    {
        if (!IsActive) return false;
        if (StartsAtUtc.HasValue && DateTime.UtcNow < StartsAtUtc.Value) return false;
        if (EndsAtUtc.HasValue && DateTime.UtcNow > EndsAtUtc.Value) return false;
        return true;
    }
}
