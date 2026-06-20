using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Vendors.Entities;

namespace Zadana.Application.Modules.Vendors.Support;

public static class VendorProfileSectionAdminAlerts
{
    public static AdminAlertRequest BuildSectionReviewRequest(Vendor vendor, string section)
    {
        var (labelAr, labelEn) = VendorProfileReviewCatalog.GetSectionLabel(section);

        return new AdminAlertRequest(
            ResolveAlertType(section),
            AdminAlertCategories.Vendors,
            ResolvePriority(section),
            $"{labelAr} جاهزة للمراجعة",
            $"{labelEn} ready for review",
            $"قام التاجر {vendor.BusinessNameAr} بتحديث {labelAr} وهي بانتظار مراجعتك.",
            $"Vendor {vendor.BusinessNameEn} updated {labelEn} and it is waiting for your review.",
            vendor.Id,
            BuildComplianceTargetUrl(vendor.Id),
            new
            {
                vendorId = vendor.Id,
                userId = vendor.UserId,
                section,
                complianceSection = section
            });
    }

    public static Task NotifySectionReviewAsync(
        IAdminAlertService adminAlertService,
        Vendor vendor,
        string section,
        CancellationToken cancellationToken = default) =>
        adminAlertService.SendAsync(BuildSectionReviewRequest(vendor, section), cancellationToken);

    private static string BuildComplianceTargetUrl(Guid vendorId) => $"/vendors/{vendorId}/compliance";

    private static string ResolveAlertType(string section) =>
        section.Trim().ToLowerInvariant() switch
        {
            "store" => AdminAlertTypes.VendorStoreUpdated,
            "owner" => AdminAlertTypes.VendorOwnerUpdated,
            "contact" => AdminAlertTypes.VendorContactUpdated,
            "legal" => AdminAlertTypes.VendorLegalUpdated,
            "banking" => AdminAlertTypes.VendorBankingUpdated,
            _ => AdminAlertTypes.VendorStoreUpdated
        };

    private static string ResolvePriority(string section) =>
        section.Trim().ToLowerInvariant() switch
        {
            "owner" or "legal" or "banking" => AdminAlertPriorities.High,
            _ => AdminAlertPriorities.Normal
        };
}
