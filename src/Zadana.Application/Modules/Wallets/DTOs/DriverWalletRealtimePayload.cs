namespace Zadana.Application.Modules.Wallets.DTOs;

public record DriverWalletRealtimePayload(
    decimal CurrentBalance,
    decimal PendingBalance,
    DriverWithdrawalSummaryDto WithdrawalSummary,
    IReadOnlyList<DriverWalletTransactionDto> RecentTransactions);
