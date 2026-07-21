using System.ComponentModel.DataAnnotations;
using Zadana.Domain.Modules.Wallets.Enums;

namespace Zadana.Application.Modules.Wallets.DTOs;

public record AdminWalletSummaryDto(
    Guid Id,
    string OwnerType,
    Guid OwnerId,
    string OwnerName,
    string OwnerPhone,
    decimal CurrentBalance,
    decimal PendingBalance,
    decimal AvailableBalance,
    decimal CodOwedBalance,
    DateTime CreatedAtUtc);

public record AdminWalletListDto(
    IReadOnlyList<AdminWalletSummaryDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    decimal TotalPlatformBalance,
    decimal TotalPendingWithdrawals);

public record AdminWalletTransactionDto(
    Guid Id,
    string TxnType,
    string Direction,
    decimal Amount,
    string? Description,
    string? ReferenceType,
    string? ReferenceId,
    DateTime CreatedAtUtc);

public record AdminWalletTransactionListDto(
    IReadOnlyList<AdminWalletTransactionDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public record AdminCreateAdjustmentRequest(
    [Required] decimal Amount,
    [Required] [RegularExpression("IN|OUT")] string Direction,
    [Required] [MaxLength(255)] string Description);

public record AdminWithdrawalRequestListDto(
    IReadOnlyList<AdminDriverWithdrawalRequestDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public record AdminDriverWithdrawalRequestDto(
    Guid Id,
    Guid DriverId,
    string DriverName,
    string DriverPhone,
    decimal Amount,
    string Status,
    string? TransferReference,
    string? FailureReason,
    DateTime CreatedAtUtc,
    DateTime? ProcessedAtUtc,
    AdminDriverPayoutMethodDto? PayoutMethod,
    Guid? PayoutId = null,
    string? ProviderName = null,
    string? ProviderTransferId = null,
    string? PayoutDay = null);

public record AdminDriverPayoutMethodDto(
    Guid Id,
    string MethodType,
    string AccountHolderName,
    string ProviderName,
    string MaskedLabel);

public record AdminProcessWithdrawalRequest(
    [Required] bool IsApproved,
    string? TransferReference,
    string? FailureReason);

/// <summary>
/// Result of an administrator's withdrawal decision.  A manual approval only
/// prepares the financial records; it is deliberately not a payment
/// confirmation. The caller must use <see cref="PayoutId"/> to claim the
/// payout, record the external bank submission, and then confirm it with its
/// reference and proof.
/// </summary>
public record AdminProcessWithdrawalResultDto(
    Guid WithdrawalId,
    string WithdrawalStatus,
    Guid? PayoutId,
    string? PayoutStatus,
    bool ManualWorkflowRequired,
    string? ManualClaimEndpoint,
    string? ManualBankSubmissionEndpoint,
    string? ManualConfirmationEndpoint,
    string? TransferReference,
    string? FailureReason);

public record AdminPlatformBankAccountDto(
    Guid? Id,
    string BankName,
    string AccountHolderName,
    string Iban,
    string? AccountNumber,
    string CountryCode,
    string City,
    bool IsActive,
    bool IsBankTransferEnabled,
    bool IsMoyasarPayoutsEnabled,
    string? MoyasarPayoutSourceId,
    string? Notes,
    DateTime? UpdatedAtUtc,
    bool CanReceiveBankTransfers,
    bool CanSendMoyasarPayouts);

public record AdminUpsertPlatformBankAccountRequest(
    [Required] [MaxLength(200)] string BankName,
    [Required] [MaxLength(200)] string AccountHolderName,
    [Required] [MaxLength(34)] string Iban,
    [MaxLength(64)] string? AccountNumber,
    [MaxLength(2)] string? CountryCode,
    [MaxLength(100)] string? City,
    bool IsBankTransferEnabled,
    bool IsMoyasarPayoutsEnabled,
    [MaxLength(100)] string? MoyasarPayoutSourceId,
    [MaxLength(500)] string? Notes);

public record AdminCreateMoyasarPayoutSourceRequest(
    string? CompanyCode,
    string? Certificate,
    string? PrivateKey);
