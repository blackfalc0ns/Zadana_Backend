namespace Zadana.Application.Modules.Vendors.DTOs;

public record AdminVendorFinanceSummaryDto(
    decimal AvailableBalance,
    decimal PendingSettlement,
    decimal HoldAmount,
    decimal TotalPaidOut,
    decimal PendingOrdersNet,
    decimal PendingOrdersGross,
    decimal PendingOrdersCommission,
    int PendingOrdersCount,
    int FailedPayoutsCount,
    int TotalSettlementsCount,
    int DirectSettlementsCount,
    int BatchSettlementsCount,
    int TotalPayoutsCount,
    string? LatestPayoutAtUtc,
    string? LatestPayoutNumber,
    decimal? LatestPayoutAmount,
    string? LatestPayoutStatus);
