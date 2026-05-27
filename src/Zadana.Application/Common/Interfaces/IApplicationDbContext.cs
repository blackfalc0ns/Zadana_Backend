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

namespace Zadana.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    // Identity
    DbSet<User> Users { get; }
    DbSet<PermissionDefinition> PermissionDefinitions { get; }
    DbSet<RoleDefinition> RoleDefinitions { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<UserAccessScope> UserAccessScopes { get; }
    DbSet<UserPermissionOverride> UserPermissionOverrides { get; }
    DbSet<AccessAuditLog> AccessAuditLogs { get; }
    DbSet<SystemLogEntry> SystemLogEntries { get; }
    DbSet<CustomerFavorite> CustomerFavorites { get; }
    DbSet<UserPushDevice> UserPushDevices { get; }

    // Vendors
    DbSet<Vendor> Vendors { get; }
    DbSet<VendorBranch> VendorBranches { get; }
    DbSet<BranchOperatingHour> BranchOperatingHours { get; }
    DbSet<VendorBankAccount> VendorBankAccounts { get; }
    DbSet<VendorDocumentReview> VendorDocumentReviews { get; }
    DbSet<VendorProfileReviewItem> VendorProfileReviewItems { get; }
    DbSet<VendorWorkspaceState> VendorWorkspaceStates { get; }
    DbSet<VendorStaffInvitation> VendorStaffInvitations { get; }
    DbSet<VendorSupportTicket> VendorSupportTickets { get; }
    DbSet<VendorSupportTicketMessage> VendorSupportTicketMessages { get; }

    // Catalog
    DbSet<Category> Categories { get; }
    DbSet<Brand> Brands { get; }
    DbSet<BrandCategory> BrandCategories { get; }
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
    DbSet<OrderSupportCase> OrderSupportCases { get; }
    DbSet<OrderSupportCaseAttachment> OrderSupportCaseAttachments { get; }
    DbSet<OrderSupportCaseActivity> OrderSupportCaseActivities { get; }

    // Payments
    DbSet<Payment> Payments { get; }
    DbSet<Refund> Refunds { get; }
    DbSet<RefundAllocation> RefundAllocations { get; }
    DbSet<PaymentProviderEventInbox> PaymentProviderEvents { get; }

    // Delivery
    DbSet<Driver> Drivers { get; }
    DbSet<DriverLocation> DriverLocations { get; }
    DbSet<DriverLatestLocation> DriverLatestLocations { get; }
    DbSet<DeliveryAssignment> DeliveryAssignments { get; }
    DbSet<DeliveryOfferAttempt> DeliveryOfferAttempts { get; }
    DbSet<DeliveryProof> DeliveryProofs { get; }
    DbSet<CustomerAddress> CustomerAddresses { get; }
    DbSet<DeliveryZone> DeliveryZones { get; }
    DbSet<DeliveryPricingRule> DeliveryPricingRules { get; }
    DbSet<DeliveryPricingSurgeWindow> DeliveryPricingSurgeWindows { get; }
    DbSet<DriverNote> DriverNotes { get; }
    DbSet<DriverIncident> DriverIncidents { get; }
    DbSet<DriverDocumentReview> DriverDocumentReviews { get; }

    // Wallets & Settlements
    DbSet<Wallet> Wallets { get; }
    DbSet<WalletTransaction> WalletTransactions { get; }
    DbSet<Settlement> Settlements { get; }
    DbSet<SettlementItem> SettlementItems { get; }
    DbSet<Payout> Payouts { get; }
    DbSet<PayoutAttempt> PayoutAttempts { get; }
    DbSet<VendorRecovery> VendorRecoveries { get; }
    DbSet<DriverRecovery> DriverRecoveries { get; }
    DbSet<DriverPayoutMethod> DriverPayoutMethods { get; }
    DbSet<DriverWithdrawalRequest> DriverWithdrawalRequests { get; }
    DbSet<WalletHold> WalletHolds { get; }
    DbSet<PlatformBankAccount> PlatformBankAccounts { get; }

    // Finances
    DbSet<CityDeliveryPricingSettings> CityDeliveryPricingSettings { get; }
    DbSet<RegionDeliveryPricingSettings> RegionDeliveryPricingSettings { get; }
    DbSet<DeliveryPricingDefaults> DeliveryPricingDefaults { get; }
    DbSet<ZoneFinanceSettings> ZoneFinanceSettings { get; }
    DbSet<FinancialEvent> FinancialEvents { get; }
    DbSet<JournalEntry> JournalEntries { get; }
    DbSet<JournalLine> JournalLines { get; }
    DbSet<PaymentGatewaySettlement> PaymentGatewaySettlements { get; }
    DbSet<PaymentGatewaySettlementItem> PaymentGatewaySettlementItems { get; }

    // Marketing & Social
    DbSet<Coupon> Coupons { get; }
    DbSet<CouponVendor> CouponVendors { get; }
    DbSet<HomeBanner> HomeBanners { get; }
    DbSet<HomeContentSectionSetting> HomeContentSectionSettings { get; }
    DbSet<EmailSenderProfileConfig> EmailSenderProfileConfigs { get; }
    DbSet<EmailWorkflowRuleConfig> EmailWorkflowRuleConfigs { get; }
    DbSet<EmailDispatchLog> EmailDispatchLogs { get; }
    DbSet<HomeSection> HomeSections { get; }
    DbSet<FeaturedProductPlacement> FeaturedProductPlacements { get; }
    DbSet<FeaturedProductSelectionSettings> FeaturedProductSelectionSettings { get; }
    DbSet<Review> Reviews { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<AdminAlertEvent> AdminAlertEvents { get; }
    DbSet<AdminAlertDispatch> AdminAlertDispatches { get; }

    // Geography
    DbSet<SaudiRegion> SaudiRegions { get; }
    DbSet<SaudiCity> SaudiCities { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
