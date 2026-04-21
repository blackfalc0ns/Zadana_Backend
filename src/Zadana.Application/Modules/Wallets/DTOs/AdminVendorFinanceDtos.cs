namespace Zadana.Application.Modules.Wallets.DTOs;

public record AdminVendorSettlementDto(
    Guid Id,
    string SettlementNumber,
    decimal GrossAmount,
    decimal CommissionAmount,
    decimal NetAmount,
    string Origin,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? ProcessedAtUtc,
    int PayoutsCount,
    int OrdersCount,
    Guid? SourceOrderId,
    string? SourceOrderNumber);

public record AdminVendorPayoutDto(
    Guid Id,
    Guid SettlementId,
    string PayoutNumber,
    decimal Amount,
    string Origin,
    string Status,
    string? TransferReference,
    DateTime CreatedAtUtc,
    DateTime? ProcessedAtUtc,
    Guid? SourceOrderId,
    string? SourceOrderNumber,
    Guid? VendorBankAccountId,
    string? BankName,
    string? AccountHolderName,
    string? Iban,
    string? SwiftCode);
