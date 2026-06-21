using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Serialization;

namespace Zadana.Application.Modules.Vendors.Queries.GetAdminVendorFinanceSummary;

public record GetAdminVendorFinanceSummaryQuery(Guid VendorId) : IRequest<AdminVendorFinanceSummaryDto>;

public class GetAdminVendorFinanceSummaryQueryHandler
    : IRequestHandler<GetAdminVendorFinanceSummaryQuery, AdminVendorFinanceSummaryDto>
{
    private static readonly SettlementStatus[] PendingSettlementStatuses =
    [
        SettlementStatus.Pending,
        SettlementStatus.PendingReview,
        SettlementStatus.Approved,
        SettlementStatus.OnHold,
        SettlementStatus.Processing
    ];

    private static readonly PayoutStatus[] FailedPayoutStatuses =
    [
        PayoutStatus.Failed,
        PayoutStatus.Cancelled
    ];

    private readonly IApplicationDbContext _context;

    public GetAdminVendorFinanceSummaryQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AdminVendorFinanceSummaryDto> Handle(
        GetAdminVendorFinanceSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var vendorExists = await _context.Vendors
            .AsNoTracking()
            .AnyAsync(vendor => vendor.Id == request.VendorId, cancellationToken);

        if (!vendorExists)
        {
            throw new NotFoundException("Vendor", request.VendorId);
        }

        var wallet = await _context.Wallets
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.OwnerType == WalletOwnerType.Vendor && item.OwnerId == request.VendorId,
                cancellationToken);

        var activeHoldAmount = await _context.WalletHolds
            .AsNoTracking()
            .Where(hold =>
                hold.OwnerType == WalletOwnerType.Vendor &&
                hold.OwnerId == request.VendorId &&
                hold.Status == WalletHoldStatus.Active)
            .SumAsync(hold => (decimal?)hold.Amount, cancellationToken) ?? 0m;

        var holdAmount = (wallet?.PendingBalance ?? 0m) + activeHoldAmount;
        var availableBalance = Math.Max(0m, (wallet?.CurrentBalance ?? 0m) - holdAmount);

        var pendingSettlement = await _context.Settlements
            .AsNoTracking()
            .Where(settlement => settlement.VendorId == request.VendorId && PendingSettlementStatuses.Contains(settlement.Status))
            .SumAsync(settlement => (decimal?)settlement.NetAmount, cancellationToken) ?? 0m;

        var totalPaidOut = await _context.Payouts
            .AsNoTracking()
            .Where(payout =>
                payout.Settlement.VendorId == request.VendorId &&
                payout.Status == PayoutStatus.Paid)
            .SumAsync(payout => (decimal?)payout.Amount, cancellationToken) ?? 0m;

        var settledOrderIds = await _context.SettlementItems
            .AsNoTracking()
            .Where(item => item.Settlement.VendorId == request.VendorId)
            .Select(item => item.OrderId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var awaitingOrders = await _context.Orders
            .AsNoTracking()
            .Where(order =>
                order.VendorId == request.VendorId &&
                order.Status == OrderStatus.Delivered &&
                !settledOrderIds.Contains(order.Id))
            .Select(order => new
            {
                order.TotalAmount,
                order.CommissionAmount
            })
            .ToListAsync(cancellationToken);

        var pendingOrdersGross = awaitingOrders.Sum(order => order.TotalAmount);
        var pendingOrdersCommission = awaitingOrders.Sum(order => order.CommissionAmount);
        var pendingOrdersNet = awaitingOrders.Sum(order => Math.Max(order.TotalAmount - order.CommissionAmount, 0m));

        var settlementsQuery = _context.Settlements
            .AsNoTracking()
            .Where(settlement => settlement.VendorId == request.VendorId);

        var totalSettlementsCount = await settlementsQuery.CountAsync(cancellationToken);
        var directSettlementsCount = await settlementsQuery
            .CountAsync(settlement => settlement.Origin == SettlementOrigin.DirectPerOrder, cancellationToken);
        var batchSettlementsCount = totalSettlementsCount - directSettlementsCount;

        var payoutsQuery = _context.Payouts
            .AsNoTracking()
            .Where(payout => payout.Settlement.VendorId == request.VendorId);

        var totalPayoutsCount = await payoutsQuery.CountAsync(cancellationToken);
        var failedPayoutsCount = await payoutsQuery
            .CountAsync(payout => FailedPayoutStatuses.Contains(payout.Status), cancellationToken);

        var latestPayout = await payoutsQuery
            .OrderByDescending(payout => payout.ProcessedAtUtc ?? payout.CreatedAtUtc)
            .Select(payout => new
            {
                payout.Id,
                payout.ProcessedAtUtc,
                payout.CreatedAtUtc,
                payout.Amount,
                payout.Status
            })
            .FirstOrDefaultAsync(cancellationToken);

        return new AdminVendorFinanceSummaryDto(
            availableBalance,
            pendingSettlement,
            holdAmount,
            totalPaidOut,
            pendingOrdersNet,
            pendingOrdersGross,
            pendingOrdersCommission,
            awaitingOrders.Count,
            failedPayoutsCount,
            totalSettlementsCount,
            directSettlementsCount,
            batchSettlementsCount,
            totalPayoutsCount,
            (latestPayout?.ProcessedAtUtc ?? latestPayout?.CreatedAtUtc) is { } latestPayoutAtUtc
                ? SaudiTime.ToSaudi(latestPayoutAtUtc).ToString("O")
                : null,
            latestPayout is null ? null : $"PAY-{latestPayout.Id.ToString("N")[..8].ToUpperInvariant()}",
            latestPayout?.Amount,
            latestPayout?.Status.ToString());
    }
}
