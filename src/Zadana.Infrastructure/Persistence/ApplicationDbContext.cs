using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
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
using Zadana.Application.Common.Interfaces;
using Zadana.Infrastructure.Persistence.Interceptors;

namespace Zadana.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>, IApplicationDbContext, IUnitOfWork
{
    private readonly AuditableEntityInterceptor _auditableInterceptor;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        AuditableEntityInterceptor auditableInterceptor)
        : base(options)
    {
        _auditableInterceptor = auditableInterceptor;
    }

    // Identity
    public new DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<CustomerFavorite> CustomerFavorites => Set<CustomerFavorite>();
    public DbSet<UserPushDevice> UserPushDevices => Set<UserPushDevice>();

    // Vendors
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<VendorBranch> VendorBranches => Set<VendorBranch>();
    public DbSet<BranchOperatingHour> BranchOperatingHours => Set<BranchOperatingHour>();
    public DbSet<VendorBankAccount> VendorBankAccounts => Set<VendorBankAccount>();
    public DbSet<VendorDocumentReview> VendorDocumentReviews => Set<VendorDocumentReview>();

    // Catalog
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<ProductType> ProductTypes => Set<ProductType>();
    public DbSet<Part> Parts => Set<Part>();
    public DbSet<UnitOfMeasure> UnitsOfMeasure => Set<UnitOfMeasure>();
    public DbSet<MasterProduct> MasterProducts => Set<MasterProduct>();
    public DbSet<AdminBrandBulkOperation> AdminBrandBulkOperations => Set<AdminBrandBulkOperation>();
    public DbSet<AdminBrandBulkOperationItem> AdminBrandBulkOperationItems => Set<AdminBrandBulkOperationItem>();
    public DbSet<AdminMasterProductBulkOperation> AdminMasterProductBulkOperations => Set<AdminMasterProductBulkOperation>();
    public DbSet<AdminMasterProductBulkOperationItem> AdminMasterProductBulkOperationItems => Set<AdminMasterProductBulkOperationItem>();
    public DbSet<VendorProduct> VendorProducts => Set<VendorProduct>();
    public DbSet<VendorProductBulkOperation> VendorProductBulkOperations => Set<VendorProductBulkOperation>();
    public DbSet<VendorProductBulkOperationItem> VendorProductBulkOperationItems => Set<VendorProductBulkOperationItem>();
    public DbSet<ProductRequest> ProductRequests => Set<ProductRequest>();
    public DbSet<BrandRequest> BrandRequests => Set<BrandRequest>();
    public DbSet<CategoryRequest> CategoryRequests => Set<CategoryRequest>();

    // Orders & Carts
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();
    public DbSet<OrderComplaint> OrderComplaints => Set<OrderComplaint>();
    public DbSet<OrderComplaintAttachment> OrderComplaintAttachments => Set<OrderComplaintAttachment>();

    // Payments
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Refund> Refunds => Set<Refund>();

    // Delivery
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<DriverLocation> DriverLocations => Set<DriverLocation>();
    public DbSet<DeliveryAssignment> DeliveryAssignments => Set<DeliveryAssignment>();
    public DbSet<DeliveryProof> DeliveryProofs => Set<DeliveryProof>();
    public DbSet<CustomerAddress> CustomerAddresses => Set<CustomerAddress>();

    // Wallets & Settlements
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();
    public DbSet<Settlement> Settlements => Set<Settlement>();
    public DbSet<SettlementItem> SettlementItems => Set<SettlementItem>();
    public DbSet<Payout> Payouts => Set<Payout>();

    // Marketing & Social
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<CouponVendor> CouponVendors => Set<CouponVendor>();
    public DbSet<HomeBanner> HomeBanners => Set<HomeBanner>();
    public DbSet<HomeContentSectionSetting> HomeContentSectionSettings => Set<HomeContentSectionSetting>();
    public DbSet<HomeSection> HomeSections => Set<HomeSection>();
    public DbSet<FeaturedProductPlacement> FeaturedProductPlacements => Set<FeaturedProductPlacement>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(_auditableInterceptor);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
