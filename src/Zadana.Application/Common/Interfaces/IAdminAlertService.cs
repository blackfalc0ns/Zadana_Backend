namespace Zadana.Application.Common.Interfaces;

public interface IAdminAlertService
{
    Task<AdminAlertDispatchResult> SendAsync(
        AdminAlertRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record AdminAlertRequest(
    string Type,
    string Category,
    string Priority,
    string TitleAr,
    string TitleEn,
    string BodyAr,
    string BodyEn,
    Guid? ReferenceId,
    string TargetUrl,
    object? Data = null,
    bool SuppressPush = false);

public sealed record AdminAlertDispatchResult(
    int RecipientCount,
    int SignalRSuccessCount,
    OneSignalPushDispatchResult PushResult)
{
    public Guid? EventId { get; init; }
    public string Status { get; init; } = "queued";
}

public static class AdminAlertTypes
{
    public const string DriverApprovalRequested = "driver.approval_requested";
    public const string DriverDocumentsSubmitted = "driver.documents_submitted";
    public const string DriverApprovalBlocked = "driver.approval_blocked";
    public const string VendorApprovalRequested = "vendor.approval_requested";
    public const string VendorDocumentsSubmitted = "vendor.documents_submitted";
    public const string VendorCriticalChangeSubmitted = "vendor.critical_change_submitted";
    public const string VendorStoreUpdated = "vendor.store_updated";
    public const string VendorLegalUpdated = "vendor.legal_updated";
    public const string VendorBankingUpdated = "vendor.banking_updated";
    public const string CatalogProductRequestSubmitted = "catalog.product_request_submitted";
    public const string CatalogBrandRequestSubmitted = "catalog.brand_request_submitted";
    public const string CatalogCategoryRequestSubmitted = "catalog.category_request_submitted";
    public const string DisputeCreated = "dispute.created";
    public const string DisputeEscalated = "dispute.escalated";
    public const string RefundRequested = "refund.requested";
    public const string SettlementRequested = "settlement.requested";
    public const string SettlementFailed = "settlement.failed";
    public const string SupportCriticalCreated = "support.critical_created";
    public const string SystemIntegrationFailure = "system.integration_failure";
    public const string SystemOneSignalFailure = "system.onesignal_failure";
}

public static class AdminAlertCategories
{
    public const string Drivers = "drivers";
    public const string Vendors = "vendors";
    public const string Catalog = "catalog";
    public const string Disputes = "disputes";
    public const string Refunds = "refunds";
    public const string Settlements = "settlements";
    public const string Support = "support";
    public const string System = "system";
}

public static class AdminAlertPriorities
{
    public const string Low = "low";
    public const string Normal = "normal";
    public const string High = "high";
    public const string Critical = "critical";
}
