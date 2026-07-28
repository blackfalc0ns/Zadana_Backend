using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Vendors.Enums;

namespace Zadana.Infrastructure.Modules.Vendors.Services;

internal static class AdminVendorQueryFilters
{
    internal sealed record VendorQueryRow(Vendor Vendor, User? User);

    public static IQueryable<VendorQueryRow> ApplyDerivedFilters(
        IQueryable<VendorQueryRow> query,
        string? riskLevel,
        string? verificationStatus,
        string? documentsStatus,
        string? payoutStatus,
        string? onboardingStage)
    {
        if (!string.IsNullOrWhiteSpace(riskLevel))
        {
            query = ApplyRiskLevel(query, riskLevel.Trim());
        }

        if (!string.IsNullOrWhiteSpace(verificationStatus))
        {
            query = ApplyVerificationStatus(query, verificationStatus.Trim());
        }

        if (!string.IsNullOrWhiteSpace(documentsStatus))
        {
            query = ApplyDocumentsStatus(query, documentsStatus.Trim());
        }

        if (!string.IsNullOrWhiteSpace(payoutStatus))
        {
            query = ApplyPayoutStatus(query, payoutStatus.Trim());
        }

        if (!string.IsNullOrWhiteSpace(onboardingStage))
        {
            query = ApplyOnboardingStage(query, onboardingStage.Trim());
        }

        return query;
    }

    private static IQueryable<VendorQueryRow> ApplyRiskLevel(IQueryable<VendorQueryRow> query, string riskLevel) =>
        riskLevel.ToUpperInvariant() switch
        {
            "HIGH" or "CRITICAL" => query.Where(item =>
                item.Vendor.Status == VendorStatus.Suspended ||
                (item.User != null && item.User.IsLoginLocked)),
            "LOW" => query.Where(item =>
                item.Vendor.Status != VendorStatus.Suspended &&
                (item.User == null || !item.User.IsLoginLocked)),
            "MEDIUM" => query.Where(item =>
                item.Vendor.Status == VendorStatus.PendingReview &&
                (item.User == null || !item.User.IsLoginLocked)),
            _ => query
        };

    private static IQueryable<VendorQueryRow> ApplyVerificationStatus(IQueryable<VendorQueryRow> query, string verificationStatus) =>
        verificationStatus.ToUpperInvariant() switch
        {
            "VERIFIED" => query.Where(item => item.Vendor.Status == VendorStatus.Active),
            "PENDING" => query.Where(item => item.Vendor.Status == VendorStatus.PendingReview),
            "UNVERIFIED" => query.Where(item =>
                item.Vendor.Status != VendorStatus.Active &&
                item.Vendor.Status != VendorStatus.PendingReview),
            _ => query
        };

    private static IQueryable<VendorQueryRow> ApplyDocumentsStatus(IQueryable<VendorQueryRow> query, string documentsStatus) =>
        documentsStatus.ToUpperInvariant() switch
        {
            "COMPLETE" => query.Where(item => item.Vendor.Status == VendorStatus.Active),
            "INCOMPLETE" => query.Where(item => item.Vendor.Status == VendorStatus.PendingReview),
            "MISSING" => query.Where(item =>
                item.Vendor.Status != VendorStatus.Active &&
                item.Vendor.Status != VendorStatus.PendingReview),
            _ => query
        };

    private static IQueryable<VendorQueryRow> ApplyPayoutStatus(IQueryable<VendorQueryRow> query, string payoutStatus) =>
        payoutStatus.ToUpperInvariant() switch
        {
            "BLOCKED" => query.Where(item => item.Vendor.Status == VendorStatus.Suspended),
            "ACTIVE" => query.Where(item => item.Vendor.Status == VendorStatus.Active),
            "PENDING" => query.Where(item => item.Vendor.Status == VendorStatus.PendingReview),
            _ => query
        };

    private static IQueryable<VendorQueryRow> ApplyOnboardingStage(IQueryable<VendorQueryRow> query, string onboardingStage) =>
        onboardingStage.ToUpperInvariant() switch
        {
            "APPROVED" => query.Where(item => item.Vendor.Status == VendorStatus.Active),
            "UNDERREVIEW" => query.Where(item => item.Vendor.Status == VendorStatus.PendingReview),
            "DOCUMENTSPENDING" => query.Where(item =>
                item.Vendor.Status != VendorStatus.Active &&
                item.Vendor.Status != VendorStatus.PendingReview),
            _ => query
        };
}
