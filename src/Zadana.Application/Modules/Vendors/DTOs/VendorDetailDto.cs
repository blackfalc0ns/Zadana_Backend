namespace Zadana.Application.Modules.Vendors.DTOs;

public record VendorDetailDto(
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
    string? RejectionReason,
    string? LogoUrl,
    string? CommercialRegisterDocumentUrl,
    DateTime? ApprovedAtUtc,
    Guid? ApprovedBy,
    DateTime CreatedAtUtc,
    // Owner info
    string OwnerName,
    string OwnerEmail,
    string OwnerPhone,
    // Counts
    int BranchesCount,
    int BankAccountsCount);
