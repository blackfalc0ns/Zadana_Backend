namespace Zadana.Application.Modules.Vendors.DTOs;

public record VendorProfileDto(
    Guid Id,
    string BusinessNameAr,
    string BusinessNameEn,
    string BusinessType,
    string CommercialRegistrationNumber,
    string? TaxId,
    string ContactEmail,
    string ContactPhone,
    decimal? CommissionRate,
    string Status,
    string? LogoUrl,
    DateTime? ApprovedAtUtc,
    DateTime CreatedAtUtc);
