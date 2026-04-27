using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Support;

public static class VendorReviewWorkflow
{
    private static readonly VendorDocumentType[] RequiredDocumentTypes =
    [
        VendorDocumentType.Commercial,
        VendorDocumentType.Tax,
        VendorDocumentType.License
    ];

    public static bool IsRequired(VendorDocumentType type) =>
        type is VendorDocumentType.Commercial or VendorDocumentType.Tax or VendorDocumentType.License;

    public static bool IsUploaded(Vendor vendor, VendorDocumentType type)
    {
        var primaryBankAccount = vendor.BankAccounts
            .OrderByDescending(account => account.IsPrimary)
            .ThenByDescending(account => account.VerifiedAtUtc)
            .ThenBy(account => account.CreatedAtUtc)
            .FirstOrDefault();

        return type switch
        {
            VendorDocumentType.Identity => !string.IsNullOrWhiteSpace(vendor.IdNumber)
                || !string.IsNullOrWhiteSpace(vendor.OwnerName)
                || !string.IsNullOrWhiteSpace(vendor.OwnerEmail)
                || !string.IsNullOrWhiteSpace(vendor.OwnerPhone)
                || !string.IsNullOrWhiteSpace(vendor.Nationality),
            VendorDocumentType.Commercial => !string.IsNullOrWhiteSpace(vendor.CommercialRegisterDocumentUrl)
                || !string.IsNullOrWhiteSpace(vendor.CommercialRegistrationNumber),
            VendorDocumentType.Tax => !string.IsNullOrWhiteSpace(vendor.TaxDocumentUrl)
                || !string.IsNullOrWhiteSpace(vendor.TaxId),
            VendorDocumentType.Bank => primaryBankAccount is not null
                && !string.IsNullOrWhiteSpace(primaryBankAccount.IBAN),
            VendorDocumentType.License => !string.IsNullOrWhiteSpace(vendor.LicenseDocumentUrl)
                || !string.IsNullOrWhiteSpace(vendor.LicenseNumber),
            _ => false
        };
    }

    public static string ResolveStatus(bool isUploaded, VendorDocumentReviewDecision? reviewDecision)
    {
        if (!isUploaded)
        {
            return "missing";
        }

        return reviewDecision == VendorDocumentReviewDecision.Approved ? "completed" : "pending";
    }

    public static string ResolveStatusLabelKey(string status) =>
        status switch
        {
            "completed" => "COMPLIANCE.STATUS.COMPLETED",
            "pending" => "COMPLIANCE.STATUS.UNDER_REVIEW",
            _ => "COMPLIANCE.STATUS.MISSING"
        };

    public static string ResolveStatusBadgeClass(string status) =>
        status switch
        {
            "completed" => "bg-teal-50 text-teal-500",
            "pending" => "bg-orange-50 text-orange-500",
            _ => "bg-slate-100 text-slate-500"
        };

    public static string ResolvePreviewKind(string? fileUrl, bool isUploaded)
    {
        if (!string.IsNullOrWhiteSpace(fileUrl))
        {
            var lowerUrl = fileUrl.ToLowerInvariant();
            if (lowerUrl.EndsWith(".png") || lowerUrl.EndsWith(".jpg") || lowerUrl.EndsWith(".jpeg") || lowerUrl.EndsWith(".webp"))
            {
                return "image";
            }

            return "pdf";
        }

        return isUploaded ? "structured" : "unavailable";
    }

    public static bool IsReadyForFinalApproval(Vendor vendor) =>
        RequiredDocumentTypes.All(type =>
        {
            if (!IsUploaded(vendor, type))
            {
                return false;
            }

            var review = vendor.DocumentReviews.FirstOrDefault(item => item.Type == type);
            return review?.Decision == VendorDocumentReviewDecision.Approved;
        });

    public static VendorDocumentReview? GetLatestRejectedRequiredReview(Vendor vendor) =>
        vendor.DocumentReviews
            .Where(item => IsRequired(item.Type) && item.Decision == VendorDocumentReviewDecision.Rejected)
            .OrderByDescending(item => item.ReviewedAtUtc)
            .ThenByDescending(item => item.UpdatedAtUtc)
            .FirstOrDefault();

    public static void EnsureComplianceActionAllowed(Vendor vendor)
    {
        if (vendor.ArchivedAtUtc.HasValue)
        {
            throw new BusinessRuleException(
                "VendorReviewArchived",
                "Cannot change compliance review for an archived vendor.");
        }

        if (vendor.Status == VendorStatus.Suspended || vendor.SuspendedAtUtc.HasValue)
        {
            throw new BusinessRuleException(
                "VendorReviewSuspended",
                "Cannot change compliance review while the vendor account is suspended.");
        }

        if (vendor.LockedAtUtc.HasValue)
        {
            throw new BusinessRuleException(
                "VendorReviewLocked",
                "Cannot change compliance review while the vendor account is locked.");
        }

        if (vendor.Status == VendorStatus.Active && vendor.ApprovedAtUtc.HasValue)
        {
            throw new BusinessRuleException(
                "VendorReviewClosed",
                "Compliance review is already closed because the vendor is approved.");
        }

        // Rejected vendors are allowed to update their profile and resubmit for review.
        // SubmitVendorReviewCommand transitions them back to PendingReview via ReopenForReview().
    }

    public static void EnsureDocumentCanBeReviewed(Vendor vendor, VendorDocumentType type)
    {
        EnsureComplianceActionAllowed(vendor);

        if (!IsUploaded(vendor, type))
        {
            throw new BusinessRuleException(
                "VendorDocumentMissing",
                "This document cannot be reviewed before it is uploaded or completed.");
        }
    }
}
