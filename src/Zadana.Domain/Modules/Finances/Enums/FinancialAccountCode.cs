namespace Zadana.Domain.Modules.Finances.Enums;

/// <summary>
/// Chart-of-accounts codes used by the platform ledger.
/// Aligned with the revised SAR-only financial workflow (sections 4 and 8 of the spec).
/// </summary>
public enum FinancialAccountCode
{
    // Assets
    PlatformCash = 0,
    GatewayReceivable = 1,
    DriverCodReceivable = 2,
    VendorRecoveryReceivable = 10,

    // Liabilities
    CustomerAdvance = 11,
    VendorPayable = 3,
    DriverPayable = 4,
    TaxPayable = 12,
    CouponLiability = 13,

    // Revenue
    PlatformRevenue = 5,

    // Expenses
    GatewayFeeExpense = 14,
    RefundExpense = 6,

    // Variance / equity-side
    SettlementVariance = 15,
    ManualAdjustment = 7,
}
