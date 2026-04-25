namespace Zadana.Application.Modules.Wallets.DTOs;

public record DriverWalletSummaryDto(
    decimal CurrentBalance,
    decimal AvailableToWithdraw,
    decimal PendingBalance,
    decimal TodayEarnings,
    decimal WeekEarnings,
    decimal MonthEarnings,
    IReadOnlyList<DriverWalletTransactionDto> RecentTransactions,
    IReadOnlyList<DriverPayoutMethodDto> PaymentMethods,
    DriverWithdrawalSummaryDto WithdrawalSummary);

public record DriverWalletTransactionListDto(
    IReadOnlyList<DriverWalletTransactionDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public record DriverWalletTransactionDto(
    Guid Id,
    string Type,
    string Direction,
    decimal Amount,
    string? Description,
    string? ReferenceType,
    string? ReferenceId,
    DateTime CreatedAtUtc);

public record DriverPayoutMethodDto(
    Guid Id,
    string Type,
    string AccountHolderName,
    string? ProviderName,
    string MaskedLabel,
    bool IsPrimary,
    bool IsVerified);

public record DriverWithdrawalSummaryDto(
    int PendingCount,
    decimal PendingAmount,
    int TotalRequests);

public record DriverWithdrawalRequestListDto(
    IReadOnlyList<DriverWithdrawalRequestDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public record DriverWithdrawalRequestDto(
    Guid Id,
    decimal Amount,
    string Status,
    string? TransferReference,
    string? FailureReason,
    DateTime CreatedAtUtc,
    DateTime? ProcessedAtUtc,
    DriverPayoutMethodDto PaymentMethod);
