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
    string ContactEmail,
    string ContactPhone,
    string? TaxId,
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
