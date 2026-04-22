namespace Zadana.Api.Modules.Vendors.Requests;

public record RegisterVendorRequest(
    string FullName,
    string Email,
    string Phone,
    string Password,
    string BusinessNameAr,
    string BusinessNameEn,
    string BusinessType,
    string CommercialRegistrationNumber,
    DateTime? CommercialRegistrationExpiryDate,
    string ContactEmail,
    string ContactPhone,
    string? DescriptionAr,
    string? DescriptionEn,
    string OwnerName,
    string OwnerEmail,
    string OwnerPhone,
    string? IdNumber,
    string? Nationality,
    string Region,
    string City,
    string NationalAddress,
    string? TaxId,
    string? LicenseNumber,
    string BankName,
    string AccountHolderName,
    string Iban,
    string? SwiftCode,
    string? PayoutCycle,
    string? LogoUrl,
    string? CommercialRegisterDocumentUrl,
    string? TaxDocumentUrl,
    string? LicenseDocumentUrl,
    string BranchName,
    string BranchAddressLine,
    decimal BranchLatitude,
    decimal BranchLongitude,
    string BranchContactPhone,
    decimal BranchDeliveryRadiusKm);

public record UpdateVendorProfileRequest(
    string BusinessNameAr,
    string BusinessNameEn,
    string BusinessType,
    string ContactEmail,
    string ContactPhone,
    string? TaxId);

public record UpdateVendorStoreRequest(
    string BusinessNameAr,
    string BusinessNameEn,
    string BusinessType,
    string ContactEmail,
    string ContactPhone,
    string? DescriptionAr,
    string? DescriptionEn,
    string? LogoUrl,
    string? CommercialRegisterDocumentUrl,
    string? Region,
    string? City,
    string? NationalAddress,
    string? CommercialRegistrationNumber);

public record UpdateVendorOwnerRequest(
    string OwnerName,
    string OwnerEmail,
    string OwnerPhone,
    string? IdNumber,
    string? Nationality);

public record UpdateVendorContactRequest(
    string Region,
    string City,
    string NationalAddress);

public record UpdateVendorLegalRequest(
    string CommercialRegistrationNumber,
    DateTime? CommercialRegistrationExpiryDate,
    string? TaxId,
    string? LicenseNumber,
    string? CommercialRegisterDocumentUrl,
    string? TaxDocumentUrl,
    string? LicenseDocumentUrl);

public record UpdateVendorBankingRequest(
    string BankName,
    string AccountHolderName,
    string Iban,
    string? SwiftCode,
    string? PayoutCycle);

public record UpdateVendorOperatingHourRequest(
    int DayOfWeek,
    string OpenTime,
    string CloseTime,
    bool IsOpen);

public record UpdateVendorHoursRequest(IReadOnlyCollection<UpdateVendorOperatingHourRequest> Hours);

public record UpdateVendorOperationsSettingsRequest(
    bool AcceptOrders,
    decimal? MinimumOrderAmount,
    int? PreparationTimeMinutes);

public record UpdateVendorNotificationSettingsRequest(
    bool EmailNotificationsEnabled,
    bool SmsNotificationsEnabled,
    bool NewOrdersNotificationsEnabled);

public record ApproveVendorRequest(decimal CommissionRate);

public record RejectVendorRequest(string Reason);

public record SuspendVendorRequest(string Reason);

public record LockVendorLoginRequest(string Reason);

public record ArchiveVendorRequest(string Reason);

public record AdminResetVendorPasswordRequest(string NewPassword);

public record AdminUpdateVendorStoreRequest(
    string BusinessNameAr,
    string BusinessNameEn,
    string BusinessType,
    string ContactEmail,
    string ContactPhone,
    string? DescriptionAr,
    string? DescriptionEn,
    string? LogoUrl,
    string? CommercialRegisterDocumentUrl,
    string? Region,
    string? City,
    string? NationalAddress,
    string? CommercialRegistrationNumber);

public record AdminUpdateVendorOwnerRequest(
    string OwnerName,
    string OwnerEmail,
    string OwnerPhone,
    string? IdNumber,
    string? Nationality);

public record AdminUpdateVendorLegalBankingRequest(
    string CommercialRegistrationNumber,
    DateTime? CommercialRegistrationExpiryDate,
    string? TaxId,
    string? LicenseNumber,
    string BankName,
    string AccountHolderName,
    string Iban,
    string? SwiftCode,
    string? PayoutCycle,
    string? CommercialRegisterDocumentUrl,
    string? TaxDocumentUrl,
    string? LicenseDocumentUrl);

public record AdminUpdateVendorContactRequest(
    string Region,
    string City,
    string NationalAddress);

public record AdminUpdateVendorHoursRequest(IReadOnlyCollection<UpdateVendorOperatingHourRequest> Hours);

public record AdminUpdateVendorOperationsSettingsRequest(
    bool AcceptOrders,
    decimal? MinimumOrderAmount,
    int? PreparationTimeMinutes);

public record AdminUpdateVendorNotificationSettingsRequest(
    bool EmailNotificationsEnabled,
    bool SmsNotificationsEnabled,
    bool NewOrdersNotificationsEnabled);

public record AdminCreateVendorSettlementRequest(
    decimal GrossAmount,
    decimal CommissionAmount,
    decimal NetAmount);

public record AdminUpdateVendorFinanceSettingsRequest(
    string FinancialLifecycleMode,
    string? PayoutCycle);

public record AdminRequestVendorDocumentsRequest(string Note);

public record AdminAddVendorReviewNoteRequest(
    string Message,
    string? AuthorName,
    string? RoleLabel);

public record AdminRejectVendorDocumentRequest(string Reason);

public sealed class AdminSendVendorNotificationRequest
{
    public string? TitleAr { get; init; }
    public string? TitleEn { get; init; }
    public string? BodyAr { get; init; }
    public string? BodyEn { get; init; }
    public string? Type { get; init; }
    public Guid? ReferenceId { get; init; }
    public string? Data { get; init; }
    public string? TargetUrl { get; init; }
    public bool SendPush { get; init; } = true;
}

public record AdminVendorNotificationResponse(
    string Message,
    Guid VendorId,
    Guid UserId,
    string ExternalId,
    string Type,
    bool InboxRequested,
    bool PushAttempted,
    bool PushSent,
    bool PushSkipped,
    int? PushStatusCode,
    string? ProviderNotificationId,
    string? PushReason);
