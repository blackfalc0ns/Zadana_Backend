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
    string? CommercialRegisterDocumentUrl);

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
    string? CommercialRegisterDocumentUrl);

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
    string? CommercialRegisterDocumentUrl);

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
    string? CommercialRegisterDocumentUrl);
