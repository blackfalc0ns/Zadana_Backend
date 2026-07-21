using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Settings;
using Zadana.Application.Modules.Finances.Services;
using Zadana.Application.Modules.Wallets.Services;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Serialization;

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
        var settlementProcessingSettingsService = scope.ServiceProvider
            .GetRequiredService<ISettlementProcessingSettingsService>();
        var enabledPayoutDays = await settlementProcessingSettingsService
            .GetEnabledPayoutDaysAsync(cancellationToken);

        var today = SaudiTime.Today;
        var vendors = await context.Vendors
            .AsNoTracking()
            .Select(v => new { v.Id, v.FinancialLifecycleMode, v.PayoutDay })
            .ToListAsync(cancellationToken);

        var eligibleVendors = vendors
            .Select(v => ResolveSchedule(
                v.Id,
                v.FinancialLifecycleMode,
                v.PayoutDay,
                today,
                settings,
                enabledPayoutDays))
            .Where(schedule => schedule is not null)
            .Select(schedule => schedule!)
            .ToList();

        if (eligibleVendors.Count == 0)
        {
            _logger.LogInformation("No vendor settlement is due on {Date}.", today);
            return;
        }

        _logger.LogInformation(
            "Processing {Count} scheduled vendor settlements for {Date}.",
            eligibleVendors.Count,
            today);

        foreach (var schedule in eligibleVendors)
        {
            try
            {
                await ProcessVendorSettlementAsync(context, vendorPayoutWalletService, schedule, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process settlement for vendor {VendorId}", schedule.VendorId);
            }
        }
    }

    private async Task ProcessVendorSettlementAsync(
        IApplicationDbContext context,
        VendorPayoutWalletService vendorPayoutWalletService,
        VendorSettlementSchedule schedule,
        CancellationToken cancellationToken)
    {
        var vendorId = schedule.VendorId;

        var settlementAlreadyCreated = await context.Settlements
            .AsNoTracking()
            .AnyAsync(
                settlement =>
                    settlement.VendorId == vendorId &&
                    settlement.Origin == SettlementOrigin.ScheduledCycle &&
                    settlement.PeriodTo == schedule.PeriodTo,
                cancellationToken);

        if (settlementAlreadyCreated)
        {
            return;
        }

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
            .OrderBy(t => t.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        if (!unsettledTxns.Any())
        {
            return;
        }

        var primaryBankAccount = await context.VendorBankAccounts
            .AsNoTracking()
            .Where(b => b.VendorId == vendorId && b.IsPrimary && b.Status == BankAccountStatus.Verified)
            .OrderByDescending(b => b.IsPrimary)
            .ThenByDescending(b => b.VerifiedAtUtc)
            .ThenByDescending(b => b.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (primaryBankAccount is null || !IsValidSaudiIban(primaryBankAccount.IBAN))
        {
            _logger.LogWarning("Vendor {VendorId} has pending settlement but no verified primary Saudi IBAN.", vendorId);
            return;
        }

        var totalNet = unsettledTxns.Sum(t => t.Amount);

        // CurrentBalance can be lower than the revenue lines that have not yet
        // been placed in a settlement (for example, because of a recovery or a
        // posted debit).  We must still link every revenue line exactly once;
        // otherwise a partial payout would make the unpaid portion disappear
        // from future cycles.  The difference is represented as a recovery on
        // this settlement and allocated deterministically (oldest revenue
        // first), so Gross - Recovery always equals the actual payout amount.
        var amountToSettle = Math.Min(totalNet, Math.Max(0m, wallet.CurrentBalance));

        if (amountToSettle <= 0)
        {
            return;
        }

        var recoveryAmount = totalNet - amountToSettle;
        var settlement = new Settlement(
            SettlementOwnerType.Vendor,
            vendorId,
            schedule.PeriodFrom,
            schedule.PeriodTo,
            SettlementOrigin.ScheduledCycle);
        settlement.UpdateTotals(
            totalNet,
            0m,
            recovery: recoveryAmount); // Commission is deducted during per-order distribution.
        context.Settlements.Add(settlement);

        var remainingRecovery = recoveryAmount;
        foreach (var txn in unsettledTxns)
        {
            var settlementItem = txn.OrderId.HasValue
                ? new SettlementItem(
                    settlement.Id,
                    txn.OrderId.Value,
                    txn.Amount,
                    0m,
                    0m,
                    0m,
                    txn.Id)
                : new SettlementItem(
                    settlement.Id,
                    SettlementItemLineType.Adjustment,
                    txn.Id,
                    orderId: null,
                    amount: txn.Amount,
                    commission: 0m,
                    refund: 0m,
                    adjustment: 0m,
                    recovery: 0m,
                    netAmount: txn.Amount,
                    walletTransactionId: txn.Id);

            if (remainingRecovery > 0m)
            {
                var itemRecovery = Math.Min(settlementItem.VendorAmount, remainingRecovery);
                settlementItem.ApplyVendorRecovery(itemRecovery);
                remainingRecovery -= itemRecovery;
            }

            context.SettlementItems.Add(settlementItem);
        }

        if (remainingRecovery > 0m)
        {
            throw new InvalidOperationException(
                $"Unable to allocate vendor recovery {remainingRecovery} for scheduled settlement {settlement.Id}.");
        }

        var payout = new Payout(settlement.Id, amountToSettle, primaryBankAccount.Id);
        payout.SetScheduledPayoutDay(schedule.PayoutDay);
        // Capture the verified destination now. A later edit to the vendor's
        // bank profile must never redirect an already prepared settlement.
        payout.PrepareDestination(
            PayoutDestinationType.VendorBankAccount,
            PayoutDestinationSnapshotCodec.CreateVendorBankAccount(primaryBankAccount));
        context.Payouts.Add(payout);

        await vendorPayoutWalletService.EnsureHoldAsync(
            vendorId,
            settlement.Id,
            amountToSettle,
            "ScheduledSettlementHold",
            "Hold for scheduled cycle settlement",
            cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Created scheduled settlement {SettlementId} for vendor {VendorId} amount {Amount}; waiting for admin approval.",
            settlement.Id,
            vendorId,
            amountToSettle);
    }

    private static bool IsValidSaudiIban(string? iban)
    {
        if (string.IsNullOrWhiteSpace(iban))
        {
            return false;
        }

        var clean = new string(iban.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return clean.Length == 24 &&
            clean.StartsWith("SA", StringComparison.OrdinalIgnoreCase) &&
            clean.Skip(2).All(char.IsDigit);
    }

    private static VendorSettlementSchedule? ResolveSchedule(
        Guid vendorId,
        VendorFinancialLifecycleMode financialLifecycleMode,
        PayoutScheduleDay payoutDay,
        DateTime today,
        FinancialSettingsOptions settings,
        IReadOnlyCollection<PayoutScheduleDay> enabledPayoutDays)
    {
        if (!PayoutScheduleDayPolicy.IsAllowed(payoutDay) ||
            !enabledPayoutDays.Contains(payoutDay) ||
            !PayoutScheduleDayPolicy.IsPayoutDay(today, payoutDay))
        {
            return null;
        }

        // Legacy records are retained for audit/history, but they are paid on
        // the weekly scheduled path and never through a per-order gateway flow.
        var effectiveMode = financialLifecycleMode == VendorFinancialLifecycleMode.PerOrderDirectPayout
            ? VendorFinancialLifecycleMode.Weekly
            : financialLifecycleMode;

        var period = effectiveMode switch
        {
            VendorFinancialLifecycleMode.Weekly => ResolveWeeklyPeriod(today, settings),
            VendorFinancialLifecycleMode.Biweekly => ResolveBiweeklyPeriod(today, settings),
            VendorFinancialLifecycleMode.Monthly => ResolveMonthlyPeriod(today, settings),
            _ => null
        };

        return period is null
            ? null
            : new VendorSettlementSchedule(vendorId, payoutDay, period.Value.From, period.Value.To);
    }

    private static (DateTime From, DateTime To) ResolveWeeklyPeriod(
        DateTime today,
        FinancialSettingsOptions settings)
    {
        var closingDay = Enum.TryParse<DayOfWeek>(settings.WeeklySettlementDayOfWeek, true, out var parsedDay)
            ? parsedDay
            : DayOfWeek.Sunday;
        var daysSinceClosing = ((int)today.DayOfWeek - (int)closingDay + 7) % 7;
        var periodTo = today.Date.AddDays(-daysSinceClosing);
        return (periodTo.AddDays(-7), periodTo);
    }

    private static (DateTime From, DateTime To)? ResolveBiweeklyPeriod(
        DateTime today,
        FinancialSettingsOptions settings)
    {
        var closingDays = settings.BiweeklySettlementDaysOfMonth
            .Where(day => day is >= 1 and <= 31)
            .Distinct()
            .OrderBy(day => day)
            .ToArray();

        if (closingDays.Length == 0)
        {
            return null;
        }

        var cutoffs = GetCycleCutoffs(today, closingDays, monthsBack: 3);
        var periodTo = cutoffs.LastOrDefault(day => day <= today.Date);
        if (periodTo == default)
        {
            return null;
        }

        var periodFrom = cutoffs.LastOrDefault(day => day < periodTo);
        return periodFrom == default ? null : (periodFrom, periodTo);
    }

    private static (DateTime From, DateTime To) ResolveMonthlyPeriod(
        DateTime today,
        FinancialSettingsOptions settings)
    {
        var currentMonthCutoff = GetMonthlyCutoff(today.Year, today.Month, settings.MonthlySettlementDayOfMonth);
        var periodTo = today.Date >= currentMonthCutoff
            ? currentMonthCutoff
            : GetMonthlyCutoff(today.AddMonths(-1).Year, today.AddMonths(-1).Month, settings.MonthlySettlementDayOfMonth);
        var previous = periodTo.AddMonths(-1);
        return (GetMonthlyCutoff(previous.Year, previous.Month, settings.MonthlySettlementDayOfMonth), periodTo);
    }

    private static IReadOnlyList<DateTime> GetCycleCutoffs(DateTime today, IReadOnlyCollection<int> days, int monthsBack) =>
        Enumerable.Range(0, monthsBack + 1)
            .SelectMany(offset =>
            {
                var month = today.AddMonths(-offset);
                var daysInMonth = DateTime.DaysInMonth(month.Year, month.Month);
                return days
                    .Where(day => day <= daysInMonth)
                    .Select(day => new DateTime(month.Year, month.Month, day));
            })
            .OrderBy(day => day)
            .ToList();

    private static DateTime GetMonthlyCutoff(int year, int month, int configuredDay)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var day = configuredDay == 0 ? daysInMonth : Math.Clamp(configuredDay, 1, daysInMonth);
        return new DateTime(year, month, day);
    }

    private sealed record VendorSettlementSchedule(
        Guid VendorId,
        PayoutScheduleDay PayoutDay,
        DateTime PeriodFrom,
        DateTime PeriodTo);
}
