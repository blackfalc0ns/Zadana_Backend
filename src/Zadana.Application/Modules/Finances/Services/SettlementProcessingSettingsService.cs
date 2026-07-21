using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Finances.Services;

public interface ISettlementProcessingSettingsService
{
    Task<SettlementProcessingSettings> GetAsync(CancellationToken cancellationToken = default);
    Task<bool> IsAutomaticAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PayoutScheduleDay>> GetEnabledPayoutDaysAsync(CancellationToken cancellationToken = default);
    Task<PayoutScheduleDay> ResolveConfiguredPayoutDayAsync(
        string? requestedPayoutDay,
        PayoutScheduleDay fallback,
        CancellationToken cancellationToken = default);
    Task EnsurePayoutDayEnabledAsync(
        PayoutScheduleDay payoutDay,
        CancellationToken cancellationToken = default);
    Task<SettlementProcessingSettings> SetModeAsync(
        SettlementProcessingMode mode,
        Guid changedByUserId,
        CancellationToken cancellationToken = default);
    Task<SettlementProcessingSettings> UpdateAsync(
        SettlementProcessingMode mode,
        IReadOnlyCollection<PayoutScheduleDay>? payoutDays,
        Guid changedByUserId,
        bool? requireManualPayoutDualControl = null,
        byte[]? expectedRowVersion = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Owns the singleton settlement processing setting. Missing rows resolve to
/// Automatic so deployments remain backwards compatible until the setting is
/// first persisted.
/// </summary>
public sealed class SettlementProcessingSettingsService : ISettlementProcessingSettingsService
{
    private readonly IApplicationDbContext _context;

    public SettlementProcessingSettingsService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SettlementProcessingSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _context.SettlementProcessingSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == SettlementProcessingSettings.SingletonId, cancellationToken);

        if (settings is not null)
        {
            return settings;
        }

        settings = new SettlementProcessingSettings(SettlementProcessingMode.Automatic);
        _context.SettlementProcessingSettings.Add(settings);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return settings;
        }
        catch (DbUpdateException)
        {
            // First use can be hit by multiple workers/admin requests at the
            // same time.  The singleton primary key is the durable winner;
            // reload it instead of turning a harmless initialization race into
            // a 500 response.
            if (_context is DbContext dbContext)
            {
                dbContext.Entry(settings).State = EntityState.Detached;
            }

            var persisted = await _context.SettlementProcessingSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == SettlementProcessingSettings.SingletonId, cancellationToken);
            if (persisted is not null)
            {
                return persisted;
            }

            throw;
        }
    }

    public async Task<bool> IsAutomaticAsync(CancellationToken cancellationToken = default) =>
        (await GetAsync(cancellationToken)).Mode == SettlementProcessingMode.Automatic;

    public async Task<IReadOnlyList<PayoutScheduleDay>> GetEnabledPayoutDaysAsync(
        CancellationToken cancellationToken = default) =>
        (await GetAsync(cancellationToken)).GetPayoutDays();

    public async Task<PayoutScheduleDay> ResolveConfiguredPayoutDayAsync(
        string? requestedPayoutDay,
        PayoutScheduleDay fallback,
        CancellationToken cancellationToken = default)
    {
        var enabledDays = await GetEnabledPayoutDaysAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(requestedPayoutDay))
        {
            return enabledDays.Contains(fallback)
                ? fallback
                : PayoutScheduleDayPolicy.ResolveFallback(enabledDays);
        }

        if (!PayoutScheduleDayPolicy.TryParse(requestedPayoutDay, out var payoutDay))
        {
            throw new BusinessRuleException(
                "INVALID_PAYOUT_DAY",
                "Payout day must be a valid day of the week.");
        }

        if (!enabledDays.Contains(payoutDay))
        {
            throw new BusinessRuleException(
                "PAYOUT_DAY_DISABLED",
                "The selected payout day is not enabled by the platform.");
        }

        return payoutDay;
    }

    public async Task EnsurePayoutDayEnabledAsync(
        PayoutScheduleDay payoutDay,
        CancellationToken cancellationToken = default)
    {
        if (!PayoutScheduleDayPolicy.IsAllowed(payoutDay))
        {
            throw new BusinessRuleException(
                "INVALID_PAYOUT_DAY",
                "Payout day must be a valid day of the week.");
        }

        if (!(await GetEnabledPayoutDaysAsync(cancellationToken)).Contains(payoutDay))
        {
            throw new BusinessRuleException(
                "PAYOUT_DAY_DISABLED",
                "The selected payout day is not enabled by the platform.");
        }
    }

    public async Task<SettlementProcessingSettings> SetModeAsync(
        SettlementProcessingMode mode,
        Guid changedByUserId,
        CancellationToken cancellationToken = default)
        => await UpdateAsync(mode, null, changedByUserId, null, null, cancellationToken);

    public async Task<SettlementProcessingSettings> UpdateAsync(
        SettlementProcessingMode mode,
        IReadOnlyCollection<PayoutScheduleDay>? payoutDays,
        Guid changedByUserId,
        bool? requireManualPayoutDualControl = null,
        byte[]? expectedRowVersion = null,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new BusinessRuleException(
                "SETTLEMENT_PROCESSING_MODE_INVALID",
                "Settlement processing mode must be Automatic or Manual.");
        }

        if (changedByUserId == Guid.Empty)
        {
            throw new BusinessRuleException(
                "USER_NOT_AUTHENTICATED",
                "An authenticated administrator is required to change settlement processing mode.");
        }

        var settings = await _context.SettlementProcessingSettings
            .FirstOrDefaultAsync(item => item.Id == SettlementProcessingSettings.SingletonId, cancellationToken);
        var isNew = settings is null;
        if (settings is null)
        {
            if (expectedRowVersion is not null)
            {
                throw new BusinessRuleException(
                    "SETTLEMENT_PROCESSING_SETTINGS_CONFLICT",
                    "Settlement processing settings were initialized or changed by another administrator. Refresh and try again.");
            }

            settings = new SettlementProcessingSettings();
            _context.SettlementProcessingSettings.Add(settings);
        }
        else if (expectedRowVersion is not null && !settings.RowVersion.SequenceEqual(expectedRowVersion))
        {
            throw new BusinessRuleException(
                "SETTLEMENT_PROCESSING_SETTINGS_CONFLICT",
                "Settlement processing settings changed since this screen was loaded. Refresh and try again.");
        }

        var currentPayoutDays = settings.GetPayoutDays();
        var normalizedPayoutDays = payoutDays is null
            ? currentPayoutDays
            : PayoutScheduleDayPolicy.NormalizeDays(payoutDays);
        if (normalizedPayoutDays.Count == 0)
        {
            throw new BusinessRuleException(
                "PAYOUT_DAYS_REQUIRED",
                "At least one payout day must be enabled.");
        }

        var modeChanged = settings.Mode != mode;
        var payoutDaysChanged = !currentPayoutDays.SequenceEqual(normalizedPayoutDays);
        var dualControlChanged = requireManualPayoutDualControl.HasValue &&
            settings.RequireManualPayoutDualControl != requireManualPayoutDualControl.Value;
        if (modeChanged || isNew)
        {
            var previousMode = settings.Mode;
            settings.SetMode(mode, changedByUserId);

            if (modeChanged)
            {
                _context.SettlementProcessingModeAudits.Add(new SettlementProcessingModeAudit(
                    previousMode,
                    mode,
                    changedByUserId));
            }
        }

        if (payoutDaysChanged)
        {
            // Do not let an administrator remove the scheduled day of a bank
            // transfer that is already owned by the manual workflow. Once the
            // bank portal submission has begun, silently moving its schedule
            // would make the proof/confirmation path impossible to complete.
            var startedManualPayouts = await _context.Payouts
                .AsNoTracking()
                .Include(item => item.ExecutionReservation)
                .Where(item =>
                    item.ScheduledPayoutDay.HasValue &&
                    !normalizedPayoutDays.Contains(item.ScheduledPayoutDay.Value) &&
                    item.ExecutionReservation != null &&
                    item.ExecutionReservation.Mode == PayoutExecutionMode.Manual &&
                    (item.ExecutionReservation.Status == PayoutExecutionReservationStatus.Claimed ||
                     item.ExecutionReservation.Status == PayoutExecutionReservationStatus.Submitted))
                .Select(item => item.Id)
                .ToListAsync(cancellationToken);
            if (startedManualPayouts.Count > 0)
            {
                throw new BusinessRuleException(
                    "PAYOUT_DAY_HAS_ACTIVE_MANUAL_TRANSFERS",
                    "A payout day cannot be disabled while manual transfers on that day are claimed or submitted. Release or reconcile them first.");
            }

            settings.SetPayoutDays(normalizedPayoutDays, changedByUserId);

            // Save the global setting and every fallback reassignment in a single
            // EF SaveChanges call. EF wraps the changes in one transaction on
            // relational providers, so there is never a persisted disabled
            // preference between the two operations.
            var fallback = PayoutScheduleDayPolicy.ResolveFallback(normalizedPayoutDays);
            var vendorsToReassign = await _context.Vendors
                .Where(item => !normalizedPayoutDays.Contains(item.PayoutDay))
                .ToListAsync(cancellationToken);
            var driversToReassign = await _context.Drivers
                .Where(item => !normalizedPayoutDays.Contains(item.PayoutDay))
                .ToListAsync(cancellationToken);

            foreach (var vendor in vendorsToReassign)
            {
                vendor.UpdatePayoutDay(fallback);
            }

            foreach (var driver in driversToReassign)
            {
                driver.UpdatePayoutDay(fallback);
            }

            // Prepared but unclaimed payouts retain an immutable schedule
            // snapshot. Reassign them with the owner preference in the same
            // transaction, while leaving submitted/automatic executions alone
            // because their funds may already be outside the platform.
            var pendingPayoutsToReassign = await _context.Payouts
                .Include(item => item.ExecutionReservation)
                .Where(item =>
                    item.ScheduledPayoutDay.HasValue &&
                    !normalizedPayoutDays.Contains(item.ScheduledPayoutDay.Value) &&
                    (item.Status == PayoutStatus.Pending || item.Status == PayoutStatus.Failed) &&
                    (item.ExecutionReservation == null ||
                     item.ExecutionReservation.Status == PayoutExecutionReservationStatus.Released))
                .ToListAsync(cancellationToken);
            foreach (var payout in pendingPayoutsToReassign)
            {
                payout.SetScheduledPayoutDay(fallback);
            }
        }

        if (dualControlChanged)
        {
            settings.SetManualPayoutDualControl(requireManualPayoutDualControl!.Value, changedByUserId);
        }

        if (isNew || modeChanged || payoutDaysChanged || dualControlChanged)
        {
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new BusinessRuleException(
                    "SETTLEMENT_PROCESSING_SETTINGS_CONFLICT",
                    "Settlement processing settings changed by another administrator. Refresh and try again.");
            }
        }

        return settings;
    }
}
