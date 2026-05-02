using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Settings;
using Zadana.Application.Modules.Wallets.Services;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;

namespace Zadana.Api.BackgroundJobs;

public sealed class VendorSettlementCycleWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<VendorSettlementCycleWorker> _logger;

    public VendorSettlementCycleWorker(
        IServiceProvider serviceProvider,
        ILogger<VendorSettlementCycleWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("VendorSettlementCycleWorker starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessScheduledSettlementsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in VendorSettlementCycleWorker.");
            }

            // Run once a day at 1:00 AM UTC (or configurable interval)
            // For simplicity in this implementation, we run every 6 hours
            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }

    private async Task ProcessScheduledSettlementsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var settings = scope.ServiceProvider.GetRequiredService<IOptions<FinancialSettingsOptions>>().Value;
        var vendorPayoutWalletService = scope.ServiceProvider.GetRequiredService<VendorPayoutWalletService>();

        var today = DateTime.UtcNow;
        
        var isWeeklyDay = today.DayOfWeek.ToString().Equals(settings.WeeklySettlementDayOfWeek, StringComparison.OrdinalIgnoreCase);
        var isBiweeklyDay = settings.BiweeklySettlementDaysOfMonth.Contains(today.Day);
        var isMonthlyDay = (settings.MonthlySettlementDayOfMonth == 0 && today.Day == DateTime.DaysInMonth(today.Year, today.Month))
                           || today.Day == settings.MonthlySettlementDayOfMonth;

        var eligibleModes = new List<VendorFinancialLifecycleMode>();
        if (isWeeklyDay) eligibleModes.Add(VendorFinancialLifecycleMode.Weekly);
        if (isBiweeklyDay) eligibleModes.Add(VendorFinancialLifecycleMode.Biweekly);
        if (isMonthlyDay) eligibleModes.Add(VendorFinancialLifecycleMode.Monthly);

        if (!eligibleModes.Any())
        {
            _logger.LogInformation("Today is not a scheduled settlement day for any cycle.");
            return;
        }

        _logger.LogInformation("Processing scheduled settlements for modes: {Modes}", string.Join(", ", eligibleModes));

        var eligibleVendors = await context.Vendors
            .AsNoTracking()
            .Where(v => eligibleModes.Contains(v.FinancialLifecycleMode))
            .Select(v => new { v.Id })
            .ToListAsync(cancellationToken);

        foreach (var vendor in eligibleVendors)
        {
            try
            {
                await ProcessVendorSettlementAsync(context, vendorPayoutWalletService, vendor.Id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process settlement for vendor {VendorId}", vendor.Id);
            }
        }
    }

    private async Task ProcessVendorSettlementAsync(
        IApplicationDbContext context,
        VendorPayoutWalletService vendorPayoutWalletService,
        Guid vendorId,
        CancellationToken cancellationToken)
    {
        var wallet = await context.Wallets
            .FirstOrDefaultAsync(w => w.OwnerType == WalletOwnerType.Vendor && w.OwnerId == vendorId, cancellationToken);

        if (wallet is null || wallet.CurrentBalance <= 0)
        {
            return;
        }

        // Find all unsettled OrderRevenue credit transactions for this vendor
        var unsettledTxns = await context.WalletTransactions
            .AsNoTracking()
            .Where(t => t.WalletId == wallet.Id && 
                        t.TxnType == WalletTxnType.OrderRevenue && 
                        t.Direction == "IN")
            .Where(t => !context.SettlementItems.Any(si => si.WalletTransactionId == t.Id))
            .ToListAsync(cancellationToken);

        if (!unsettledTxns.Any())
        {
            return;
        }

        var primaryBankAccount = await context.VendorBankAccounts
            .AsNoTracking()
            .Where(b => b.VendorId == vendorId)
            .OrderByDescending(b => b.IsPrimary)
            .ThenByDescending(b => b.VerifiedAtUtc)
            .ThenByDescending(b => b.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (primaryBankAccount is null)
        {
            _logger.LogWarning("Vendor {VendorId} has pending settlement but no active bank account.", vendorId);
            return;
        }

        var totalNet = unsettledTxns.Sum(t => t.Amount);

        // It shouldn't exceed CurrentBalance unless there were manual debits
        var amountToSettle = Math.Min(totalNet, wallet.CurrentBalance);

        if (amountToSettle <= 0)
        {
            return;
        }

        var settlement = new Settlement(vendorId, null, SettlementOrigin.ScheduledCycle);
        settlement.UpdateTotals(amountToSettle, 0m); // In scheduled, Commission is already deducted during per-order distribution
        context.Settlements.Add(settlement);

        foreach (var txn in unsettledTxns)
        {
            if (txn.OrderId.HasValue)
            {
                context.SettlementItems.Add(new SettlementItem(
                    settlement.Id, 
                    txn.OrderId.Value, 
                    txn.Amount, 
                    0m, 
                    0m, 
                    0m,
                    txn.Id));
            }
        }

        var payout = new Payout(settlement.Id, amountToSettle, primaryBankAccount.Id);
        context.Payouts.Add(payout);

        await vendorPayoutWalletService.EnsureHoldAsync(
            vendorId,
            settlement.Id,
            amountToSettle,
            "ScheduledSettlementHold",
            "Hold for scheduled cycle settlement",
            cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Created scheduled settlement {SettlementId} for vendor {VendorId} amount {Amount}", settlement.Id, vendorId, amountToSettle);
    }
}
