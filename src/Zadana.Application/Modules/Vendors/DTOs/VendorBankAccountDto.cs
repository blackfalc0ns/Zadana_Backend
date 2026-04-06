namespace Zadana.Application.Modules.Vendors.DTOs;

public record VendorBankAccountDto(
    Guid Id,
    string BankName,
    string AccountHolderName,
    string Iban,
    string? SwiftCode,
    bool IsPrimary,
    string Status,
    string? RejectionReason,
    DateTime? VerifiedAtUtc);
