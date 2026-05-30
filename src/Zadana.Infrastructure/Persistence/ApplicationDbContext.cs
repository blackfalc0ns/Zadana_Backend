using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Geography.Entities;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Marketing.Entities;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Payments.Entities;
using Zadana.Domain.Modules.Social.Entities;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Finances.Entities;
using Zadana.Application.Common.Interfaces;
using Zadana.Infrastructure.Persistence.Encryption;
using Zadana.Infrastructure.Persistence.Interceptors;

namespace Zadana.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>, IApplicationDbContext, IUnitOfWork
{
    /// <summary>
    /// The single public constructor required by <see cref="DbContextPool{TContext}"/>.
    /// Interceptors are wired via <see cref="DbContextOptionsBuilder.AddInterceptors"/>
    /// at registration time. The static <see cref="AmbientDataProtectionProvider"/>
    /// is set once at startup and reused by every pooled instance, so PII column
    /// converters keep working without flowing per-request state through the context.
    /// </summary>
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Backwards-compatible constructor for tests and design-time tooling that
    /// build the context manually. Marked <c>internal</c> so the DbContext pool
    /// (which requires exactly one public constructor) is not confused by it.
    /// Test projects access it via <c>[assembly: InternalsVisibleTo]</c>.
    /// </summary>
    internal ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        AuditableEntityInterceptor auditableInterceptor)
        : this(BuildOptionsWithInterceptor(options, auditableInterceptor))
    {
    }

    /// <summary>
    /// Backwards-compatible overload kept for callers that previously passed a
    /// <see cref="IDataProtectionProvider"/> directly. Equivalent to setting
    /// <see cref="AmbientDataProtectionProvider"/> at startup.
    /// </summary>
    internal ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        AuditableEntityInterceptor auditableInterceptor,
        IDataProtectionProvider dataProtectionProvider)
        : this(BuildOptionsWithInterceptor(options, auditableInterceptor))
    {
        AmbientDataProtectionProvider ??= dataProtectionProvider;
    }

    private static DbContextOptions<ApplicationDbContext> BuildOptionsWithInterceptor(
        DbContextOptions<ApplicationDbContext> options,
        AuditableEntityInterceptor interceptor)
    {
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>(options);
        builder.AddInterceptors(interceptor);
        return builder.Options;
    }

    // Identity
    public new DbSet<User> Users => Set<User>();
    public DbSet<PermissionDefinition> PermissionDefinitions => Set<PermissionDefinition>();
    public DbSet<RoleDefinition> RoleDefinitions => Set<RoleDefinition>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserAccessScope> UserAccessScopes => Set<UserAccessScope>();
    public DbSet<UserPermissionOverride> UserPermissionOverrides => Set<UserPermissionOverride>();
    public DbSet<AccessApprovalRequest> AccessApprovalRequests => Set<AccessApprovalRequest>();
    public DbSet<AccessAuditLog> AccessAuditLogs => Set<AccessAuditLog>();
    public DbSet<SystemLogEntry> SystemLogEntries => Set<SystemLogEntry>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<CustomerFavorite> CustomerFavorites => Set<CustomerFavorite>();
    public DbSet<UserPushDevice> UserPushDevices => Set<UserPushDevice>();

    // Vendors
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<VendorBranch> VendorBranches => Set<VendorBranch>();
    public DbSet<BranchOperatingHour> BranchOperatingHours => Set<BranchOperatingHour>();
    public DbSet<VendorBankAccount> VendorBankAccounts => Set<VendorBankAccount>();
    public DbSet<VendorDocumentReview> VendorDocumentReviews => Set<VendorDocumentReview>();
    public DbSet<VendorProfileReviewItem> VendorProfileReviewItems => Set<VendorProfileReviewItem>();
    public DbSet<VendorWorkspaceState> VendorWorkspaceStates => Set<VendorWorkspaceState>();
    public DbSet<VendorStaffInvitation> VendorStaffInvitations => Set<VendorStaffInvitation>();
    public DbSet<VendorSupportTicket> VendorSupportTickets => Set<VendorSupportTicket>();
    public DbSet<VendorSupportTicketMessage> VendorSupportTicketMessages => Set<VendorSupportTicketMessage>();

    // Catalog
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<BrandCategory> BrandCategories => Set<BrandCategory>();
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
    public DbSet<OrderSupportCase> OrderSupportCases => Set<OrderSupportCase>();
    public DbSet<OrderSupportCaseAttachment> OrderSupportCaseAttachments => Set<OrderSupportCaseAttachment>();
    public DbSet<OrderSupportCaseActivity> OrderSupportCaseActivities => Set<OrderSupportCaseActivity>();

    // Payments
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<RefundAllocation> RefundAllocations => Set<RefundAllocation>();
    public DbSet<PaymentProviderEventInbox> PaymentProviderEvents => Set<PaymentProviderEventInbox>();

    // Delivery
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<DriverLocation> DriverLocations => Set<DriverLocation>();
    public DbSet<DriverLatestLocation> DriverLatestLocations => Set<DriverLatestLocation>();
    public DbSet<DeliveryAssignment> DeliveryAssignments => Set<DeliveryAssignment>();
    public DbSet<DeliveryOfferAttempt> DeliveryOfferAttempts => Set<DeliveryOfferAttempt>();
    public DbSet<DeliveryProof> DeliveryProofs => Set<DeliveryProof>();
    public DbSet<CustomerAddress> CustomerAddresses => Set<CustomerAddress>();
    public DbSet<DeliveryZone> DeliveryZones => Set<DeliveryZone>();
    public DbSet<DeliveryPricingRule> DeliveryPricingRules => Set<DeliveryPricingRule>();
    public DbSet<DeliveryPricingSurgeWindow> DeliveryPricingSurgeWindows => Set<DeliveryPricingSurgeWindow>();
    public DbSet<DriverNote> DriverNotes => Set<DriverNote>();
    public DbSet<DriverIncident> DriverIncidents => Set<DriverIncident>();
    public DbSet<DriverDocumentReview> DriverDocumentReviews => Set<DriverDocumentReview>();

    // Wallets & Settlements
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();
    public DbSet<Settlement> Settlements => Set<Settlement>();
    public DbSet<SettlementItem> SettlementItems => Set<SettlementItem>();
    public DbSet<Payout> Payouts => Set<Payout>();
    public DbSet<PayoutAttempt> PayoutAttempts => Set<PayoutAttempt>();
    public DbSet<VendorRecovery> VendorRecoveries => Set<VendorRecovery>();
    public DbSet<DriverRecovery> DriverRecoveries => Set<DriverRecovery>();
    public DbSet<DriverPayoutMethod> DriverPayoutMethods => Set<DriverPayoutMethod>();
    public DbSet<DriverWithdrawalRequest> DriverWithdrawalRequests => Set<DriverWithdrawalRequest>();
    public DbSet<WalletHold> WalletHolds => Set<WalletHold>();
    public DbSet<PlatformBankAccount> PlatformBankAccounts => Set<PlatformBankAccount>();

    // Finances
    public DbSet<CityDeliveryPricingSettings> CityDeliveryPricingSettings => Set<CityDeliveryPricingSettings>();
    public DbSet<RegionDeliveryPricingSettings> RegionDeliveryPricingSettings => Set<RegionDeliveryPricingSettings>();
    public DbSet<DeliveryPricingDefaults> DeliveryPricingDefaults => Set<DeliveryPricingDefaults>();
    public DbSet<ZoneFinanceSettings> ZoneFinanceSettings => Set<ZoneFinanceSettings>();
    public DbSet<FinancialEvent> FinancialEvents => Set<FinancialEvent>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalLine> JournalLines => Set<JournalLine>();
    public DbSet<PaymentGatewaySettlement> PaymentGatewaySettlements => Set<PaymentGatewaySettlement>();
    public DbSet<PaymentGatewaySettlementItem> PaymentGatewaySettlementItems => Set<PaymentGatewaySettlementItem>();

    // Marketing & Social
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<CouponVendor> CouponVendors => Set<CouponVendor>();
    public DbSet<HomeBanner> HomeBanners => Set<HomeBanner>();
    public DbSet<HomeContentSectionSetting> HomeContentSectionSettings => Set<HomeContentSectionSetting>();
    public DbSet<EmailSenderProfileConfig> EmailSenderProfileConfigs => Set<EmailSenderProfileConfig>();
    public DbSet<EmailWorkflowRuleConfig> EmailWorkflowRuleConfigs => Set<EmailWorkflowRuleConfig>();
    public DbSet<EmailDispatchLog> EmailDispatchLogs => Set<EmailDispatchLog>();
    public DbSet<HomeSection> HomeSections => Set<HomeSection>();
    public DbSet<FeaturedProductPlacement> FeaturedProductPlacements => Set<FeaturedProductPlacement>();
    public DbSet<FeaturedProductSelectionSettings> FeaturedProductSelectionSettings => Set<FeaturedProductSelectionSettings>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AdminAlertEvent> AdminAlertEvents => Set<AdminAlertEvent>();
    public DbSet<AdminAlertDispatch> AdminAlertDispatches => Set<AdminAlertDispatch>();

    // Geography
    public DbSet<SaudiRegion> SaudiRegions => Set<SaudiRegion>();
    public DbSet<SaudiCity> SaudiCities => Set<SaudiCity>();

    /// <summary>
    /// Ambient DataProtection provider used by <see cref="OnModelCreating"/>
    /// to wire PII column converters. Set once at application startup before
    /// the first request — DataProtection is a process-wide singleton, so it
    /// is safe to reuse across pooled DbContext instances.
    /// </summary>
    public static IDataProtectionProvider? AmbientDataProtectionProvider { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);

        // Global soft-delete filter for catalog entities
        modelBuilder.Entity<Zadana.Domain.Modules.Catalog.Entities.MasterProduct>()
            .HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Zadana.Domain.Modules.Catalog.Entities.Brand>()
            .HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Zadana.Domain.Modules.Catalog.Entities.Category>()
            .HasQueryFilter(e => !e.IsDeleted);

        // PII encryption at rest. Skipped when no provider is wired (tests
        // / design-time tooling) so EnsureCreated keeps working.
        if (AmbientDataProtectionProvider is not null)
        {
            var converter = PiiProtector.CreateConverter(AmbientDataProtectionProvider);

            modelBuilder.Entity<Driver>()
                .Property(d => d.NationalId).HasConversion(converter);
            modelBuilder.Entity<Driver>()
                .Property(d => d.LicenseNumber).HasConversion(converter);
            modelBuilder.Entity<Driver>()
                .Property(d => d.VehicleLicenseNumber).HasConversion(converter);

            modelBuilder.Entity<VendorBankAccount>()
                .Property(a => a.IBAN).HasConversion(converter);
            modelBuilder.Entity<VendorBankAccount>()
                .Property(a => a.AccountHolderName).HasConversion(converter);
        }
    }

    /// <summary>
    /// Intercepts <see cref="EntityState.Deleted"/> entries that implement
    /// <see cref="ISoftDeletable"/> and converts them to soft deletes instead.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<Zadana.SharedKernel.Primitives.ISoftDeletable>()
                     .Where(e => e.State == EntityState.Deleted))
        {
            entry.State = EntityState.Modified;
            entry.Entity.SoftDelete();
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
