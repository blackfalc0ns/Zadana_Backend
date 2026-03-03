using Zadana.Domain.Modules.Vendors.Entities;

namespace Zadana.Domain.Modules.Marketing.Entities;

public class CouponVendor
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid CouponId { get; private set; }
    public Guid VendorId { get; private set; }

    // Navigation
    public Coupon Coupon { get; private set; } = null!;
    public Vendor Vendor { get; private set; } = null!;

    private CouponVendor() { }

    public CouponVendor(Guid couponId, Guid vendorId)
    {
        CouponId = couponId;
        VendorId = vendorId;
    }
}
