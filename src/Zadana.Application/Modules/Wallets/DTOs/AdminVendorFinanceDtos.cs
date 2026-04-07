namespace Zadana.Application.Modules.Wallets.DTOs;

public record AdminVendorSettlementDto(
    Guid Id,
    string SettlementNumber,
    decimal GrossAmount,
    decimal CommissionAmount,
    decimal NetAmount,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? ProcessedAtUtc,
    int PayoutsCount);

public record AdminVendorPayoutDto(
    Guid Id,
    Guid SettlementId,
    string PayoutNumber,
    decimal Amount,
    string Status,
    string? TransferReference,
    DateTime CreatedAtUtc,
    DateTime? ProcessedAtUtc,
    Guid? VendorBankAccountId,
    string? BankName,
    string? AccountHolderName,
    string? Iban,
    string? SwiftCode);
