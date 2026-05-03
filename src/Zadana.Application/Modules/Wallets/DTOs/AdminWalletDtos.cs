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
    AdminDriverPayoutMethodDto? PayoutMethod);

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
