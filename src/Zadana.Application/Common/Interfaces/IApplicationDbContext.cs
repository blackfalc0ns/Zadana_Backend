using Microsoft.EntityFrameworkCore;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Marketing.Entities;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Payments.Entities;
using Zadana.Domain.Modules.Social.Entities;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Wallets.Entities;

namespace Zadana.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    // Identity
    DbSet<User> Users { get; }
    DbSet<CustomerFavorite> CustomerFavorites { get; }
    DbSet<UserPushDevice> UserPushDevices { get; }

    // Vendors
    DbSet<Vendor> Vendors { get; }
    DbSet<VendorBranch> VendorBranches { get; }
    DbSet<BranchOperatingHour> BranchOperatingHours { get; }
    DbSet<VendorBankAccount> VendorBankAccounts { get; }
    DbSet<VendorDocumentReview> VendorDocumentReviews { get; }

    // Catalog
    DbSet<Category> Categories { get; }
    DbSet<Brand> Brands { get; }
    DbSet<ProductType> ProductTypes { get; }
    DbSet<Part> Parts { get; }
    DbSet<UnitOfMeasure> UnitsOfMeasure { get; }
    DbSet<MasterProduct> MasterProducts { get; }
    DbSet<AdminBrandBulkOperation> AdminBrandBulkOperations { get; }
    DbSet<AdminBrandBulkOperationItem> AdminBrandBulkOperationItems { get; }
    DbSet<AdminMasterProductBulkOperation> AdminMasterProductBulkOperations { get; }
    DbSet<AdminMasterProductBulkOperationItem> AdminMasterProductBulkOperationItems { get; }
    DbSet<VendorProduct> VendorProducts { get; }
    DbSet<VendorProductBulkOperation> VendorProductBulkOperations { get; }
    DbSet<VendorProductBulkOperationItem> VendorProductBulkOperationItems { get; }
    DbSet<ProductRequest> ProductRequests { get; }
    DbSet<BrandRequest> BrandRequests { get; }
    DbSet<CategoryRequest> CategoryRequests { get; }

    // Orders & Carts
    DbSet<Cart> Carts { get; }
    DbSet<CartItem> CartItems { get; }
    DbSet<Order> Orders { get; }
    DbSet<OrderItem> OrderItems { get; }
    DbSet<OrderStatusHistory> OrderStatusHistories { get; }
    DbSet<OrderComplaint> OrderComplaints { get; }
    DbSet<OrderComplaintAttachment> OrderComplaintAttachments { get; }

    // Payments
    DbSet<Payment> Payments { get; }
    DbSet<Refund> Refunds { get; }

    // Delivery
    DbSet<Driver> Drivers { get; }
    DbSet<DriverLocation> DriverLocations { get; }
    DbSet<DeliveryAssignment> DeliveryAssignments { get; }
    DbSet<DeliveryProof> DeliveryProofs { get; }
    DbSet<CustomerAddress> CustomerAddresses { get; }
    DbSet<DeliveryZone> DeliveryZones { get; }
    DbSet<DriverNote> DriverNotes { get; }
    DbSet<DriverIncident> DriverIncidents { get; }

    // Wallets & Settlements
    DbSet<Wallet> Wallets { get; }
    DbSet<WalletTransaction> WalletTransactions { get; }
    DbSet<Settlement> Settlements { get; }
    DbSet<SettlementItem> SettlementItems { get; }
    DbSet<Payout> Payouts { get; }

    // Marketing & Social
    DbSet<Coupon> Coupons { get; }
    DbSet<CouponVendor> CouponVendors { get; }
    DbSet<HomeBanner> HomeBanners { get; }
    DbSet<HomeContentSectionSetting> HomeContentSectionSettings { get; }
    DbSet<HomeSection> HomeSections { get; }
    DbSet<FeaturedProductPlacement> FeaturedProductPlacements { get; }
    DbSet<Review> Reviews { get; }
    DbSet<Notification> Notifications { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
