using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Settings;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Delivery.Support;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.SharedKernel.Serialization;
using Zadana.Domain.Modules.Wallets.Enums;

namespace Zadana.Infrastructure.Modules.Delivery.Services;

public class DriverReadService : IDriverReadService
{
    private sealed record AssignmentStatsRow(Guid DriverId, int Total, int Completed, int Closed);
    private sealed record DriverDocumentApprovalOverlay(
        AccessApprovalStatus Status,
        string? RejectionReason,
        DateTime? DecidedAtUtc,
        IReadOnlySet<DriverDocumentType> DocumentTypes,
        DriverDocumentsProfileChangePayload Payload);

    private sealed record DriverVehicleApprovalOverlay(
        AccessApprovalStatus Status,
        DriverVehicleProfileChangePayload Payload);

    private sealed record EffectiveVehicleProfileFields(
        DriverVehicleType? VehicleType,
        string? NationalId,
        string? LicenseNumber,
        DateTime? NationalIdExpiryDate,
        DateTime? DriverLicenseExpiryDate,
        string? VehicleLicenseNumber,
        DateTime? VehicleLicenseExpiryDate,
        string? Region,
        string? City);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IApplicationDbContext _context;
    private readonly IDriverCommitmentPolicyService _driverCommitmentPolicyService;
    private readonly INotificationService _notificationService;
    private readonly IOneSignalPushService _oneSignalPushService;
    private readonly FinancialSettingsOptions _financialSettings;

    public DriverReadService(
        IApplicationDbContext context,
        IDriverCommitmentPolicyService driverCommitmentPolicyService,
        INotificationService notificationService,
        IOneSignalPushService oneSignalPushService,
        IOptions<FinancialSettingsOptions>? financialSettings = null)
    {
        _context = context;
        _driverCommitmentPolicyService = driverCommitmentPolicyService;
        _notificationService = notificationService;
        _oneSignalPushService = oneSignalPushService;
        _financialSettings = financialSettings?.Value ?? new FinancialSettingsOptions();
    }

    public async Task<AdminDriversListDto> GetAdminDriversAsync(
        string? search, string? city, string? status, string? verificationStatus,
        string? vehicleType, string? performance, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Drivers
            .Include(d => d.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            // Avoid LOWER() to keep these searches index-friendly. The default
            // collation is case-insensitive, so LIKE handles this directly.
            var term = search.Trim();
            var like = $"%{term.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]")}%";

            // Try to look up by NationalId via the searchable hash so admins
            // can search the encrypted column directly. Equality search only —
            // partial matches on encrypted PII are not supported by design.
            var nationalIdHash = Zadana.Domain.Modules.Identity.Services
                .SearchableHashProvider.Compute(term);

            query = query.Where(d =>
                EF.Functions.Like(d.User.FullName, like) ||
                (d.User.PhoneNumber != null && EF.Functions.Like(d.User.PhoneNumber, like)) ||
                (nationalIdHash != null && d.NationalIdHash == nationalIdHash));
        }

        if (!string.IsNullOrWhiteSpace(city))
            query = query.Where(d => d.City == city);

        if (!string.IsNullOrWhiteSpace(verificationStatus) && Enum.TryParse<DriverVerificationStatus>(verificationStatus, true, out var verEnum))
            query = query.Where(d => d.VerificationStatus == verEnum);

        if (!string.IsNullOrWhiteSpace(vehicleType))
        {
            if (TryParseVehicleType(vehicleType, out var vehicleTypeEnum))
            {
                query = query.Where(d => d.VehicleType == vehicleTypeEnum);
            }
            else
            {
                query = query.Where(d => false);
            }
        }

        var drivers = await query
            .OrderByDescending(d => d.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var hasExpiryLocks = false;
        foreach (var driver in drivers)
        {
            hasExpiryLocks |= driver.ApplyDocumentExpiryLock();
        }

        if (hasExpiryLocks)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        var driverIds = drivers.Select(d => d.Id).ToList();

        // Load active task counts
        var activeTaskCounts = await _context.DeliveryAssignments
            .Where(a => driverIds.Contains(a.DriverId!.Value) &&
                DeliveryActiveAssignmentRules.OpenAssignmentStatuses.Contains(a.Status) &&
                !DeliveryActiveAssignmentRules.TerminalOrderStatuses.Contains(a.Order.Status))
            .GroupBy(a => a.DriverId)
            .Select(g => new { DriverId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.DriverId!.Value, g => g.Count, cancellationToken);

        // Load completed task counts
        var completedTaskCounts = await _context.DeliveryAssignments
            .Where(a => driverIds.Contains(a.DriverId!.Value) && a.Status == AssignmentStatus.Delivered)
            .GroupBy(a => a.DriverId)
            .Select(g => new { DriverId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.DriverId!.Value, g => g.Count, cancellationToken);

        // Load latest GPS timestamps
        var latestGps = await _context.DriverLocations
            .Where(l => driverIds.Contains(l.DriverId))
            .GroupBy(l => l.DriverId)
            .Select(g => new { DriverId = g.Key, LastSeen = g.Max(l => l.RecordedAtUtc) })
            .ToDictionaryAsync(g => g.DriverId, g => g.LastSeen, cancellationToken);

        // Load wallet balances + COD owed for collection status
        var walletRows = await _context.Wallets
            .AsNoTracking()
            .Where(w => w.OwnerType == WalletOwnerType.Driver && driverIds.Contains(w.OwnerId))
            .Select(w => new { w.OwnerId, w.CurrentBalance, w.CodOwedBalance })
            .ToListAsync(cancellationToken);
        var walletBalances = walletRows.ToDictionary(w => w.OwnerId, w => w.CurrentBalance);
        var codOwedBalances = walletRows.ToDictionary(w => w.OwnerId, w => w.CodOwedBalance);
        var codBlockThreshold = _financialSettings.DriverCodBlockThresholdAmount;

        var commitmentSummaries = await _driverCommitmentPolicyService.GetDriverSummariesAsync(driverIds, cancellationToken);

        var items = drivers.Select(d =>
        {
            activeTaskCounts.TryGetValue(d.Id, out var activeTasks);
            completedTaskCounts.TryGetValue(d.Id, out var completedTasks);
            latestGps.TryGetValue(d.Id, out var lastSeen);
            walletBalances.TryGetValue(d.Id, out var walletBalance);
            codOwedBalances.TryGetValue(d.Id, out var codOwedBalance);
            commitmentSummaries.TryGetValue(d.Id, out var commitmentSummary);
            commitmentSummary ??= new DriverCommitmentSummaryDto(0, 0, 0, 0, 0, 100m, "Healthy", true, null, null);

            var totalAssignments = activeTasks + completedTasks;
            var acceptanceRate = totalAssignments > 0 ? (decimal)completedTasks / totalAssignments * 100 : 0;

            return new AdminDriverListItemDto(
                Id: d.Id,
                DriverDisplayId: FormatDriverDisplayId(d.Id),
                FirstName: d.User.FullName.Split(' ').FirstOrDefault() ?? d.User.FullName,
                LastName: string.Join(' ', d.User.FullName.Split(' ').Skip(1)),
                PhoneNumber: d.User.PhoneNumber ?? "",
                ImageUrl: d.PersonalPhotoUrl,
                City: d.City ?? "",
                Status: MapDriverStatus(d, activeTasks),
                VerificationStatus: d.VerificationStatus.ToString(),
                ActiveTasks: activeTasks,
                CompletedTasks: completedTasks,
                AcceptanceRate: Math.Round(acceptanceRate, 0),
                WalletBalance: walletBalance,
                Performance: DerivePerformance(acceptanceRate),
                VehicleType: d.VehicleType,
                LastSeenAt: lastSeen != default ? lastSeen : d.UpdatedAtUtc,
                CommitmentScore: commitmentSummary.CommitmentScore,
                DailyRejections: commitmentSummary.DailyRejections,
                WeeklyRejections: commitmentSummary.WeeklyRejections,
                EnforcementLevel: commitmentSummary.EnforcementLevel,
                LastOfferResponseAtUtc: commitmentSummary.LastOfferResponseAtUtc,
                CanReceiveOffers: commitmentSummary.CanReceiveOffers,
                IsLoginLocked: d.User.IsLoginLocked,
                LocationUpdatesBlocked: d.IsLocationUpdatesBlocked,
                Issues: DeriveIssues(d, walletBalance, commitmentSummary),
                CollectionPaymentStatus: ResolveCollectionPaymentStatus(codOwedBalance, codBlockThreshold),
                Alerts: null);
        });

        if (!string.IsNullOrWhiteSpace(status))
        {
            items = items.Where(item => string.Equals(item.Status, status.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(performance))
        {
            items = items.Where(item => string.Equals(item.Performance, performance.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        var materializedItems = items.ToArray();
        var totalCount = materializedItems.Length;
        var pagedItems = materializedItems
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArray();

        // KPIs
        var kpis = new AdminDriverKPIsDto(
            Total: totalCount,
            OnlineNow: materializedItems.Count(i => i.Status == "Online"),
            OnMission: materializedItems.Count(i => i.Status == "OnMission"),
            UnderReview: materializedItems.Count(i => i.VerificationStatus is nameof(DriverVerificationStatus.UnderReview) or nameof(DriverVerificationStatus.NeedsDocuments)),
            Suspended: materializedItems.Count(i => i.Status == "Suspended"),
            LowPerformance: materializedItems.Count(i => i.Performance is "Low" or "NeedsImprovement"));

        return new AdminDriversListDto(pagedItems, totalCount, page, pageSize, kpis);
    }

    public async Task<AdminDriverDetailDto?> GetAdminDriverDetailAsync(Guid driverId, CancellationToken cancellationToken = default)
    {
        var driver = await _context.Drivers
            .Include(d => d.User)
            .Include(d => d.Notes)
            .Include(d => d.Incidents)
            .Include(d => d.DocumentReviews)
            .FirstOrDefaultAsync(d => d.Id == driverId, cancellationToken);

        if (driver is null) return null;

        if (driver.ApplyDocumentExpiryLock())
        {
            await _context.SaveChangesAsync(cancellationToken);
            await DriverExpiryLockNotificationDispatcher.NotifyAsync(
                driver,
                _notificationService,
                _oneSignalPushService,
                cancellationToken);
        }

        var missingRequirements = DriverProfileReadinessFactory.GetMissingRequirements(driver, driver.User);

        // Active/completed tasks
        var activeTasks = await _context.DeliveryAssignments
            .CountAsync(a => a.DriverId == driverId &&
                DeliveryActiveAssignmentRules.OpenAssignmentStatuses.Contains(a.Status) &&
                !DeliveryActiveAssignmentRules.TerminalOrderStatuses.Contains(a.Order.Status),
                cancellationToken);

        var completedTasks = await _context.DeliveryAssignments
            .CountAsync(a => a.DriverId == driverId && a.Status == AssignmentStatus.Delivered, cancellationToken);

        var totalAssignments = await _context.DeliveryAssignments
            .CountAsync(a => a.DriverId == driverId, cancellationToken);

        var terminalAssignments = await _context.DeliveryAssignments
            .CountAsync(a => a.DriverId == driverId &&
                (a.Status == AssignmentStatus.Delivered ||
                 a.Status == AssignmentStatus.Failed ||
                 a.Status == AssignmentStatus.Cancelled ||
                 a.Status == AssignmentStatus.Returned), cancellationToken);

        var acceptanceRate = totalAssignments > 0 ? (decimal)completedTasks / totalAssignments * 100 : 0;
        var completionRate = terminalAssignments > 0 ? (decimal)completedTasks / terminalAssignments * 100 : 0;

        // Latest GPS
        var lastLocation = await _context.DriverLocations
            .Where(l => l.DriverId == driverId)
            .OrderByDescending(l => l.RecordedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        // Wallet
        var wallet = await _context.Wallets
            .FirstOrDefaultAsync(w => w.OwnerType == WalletOwnerType.Driver && w.OwnerId == driverId, cancellationToken);

        var walletBalance = wallet?.CurrentBalance ?? 0;
        var pendingBalance = wallet?.PendingBalance ?? 0;
        var codOwedBalance = wallet?.CodOwedBalance ?? 0;
        var codBlockThreshold = _financialSettings.DriverCodBlockThresholdAmount;

        var activeHoldTotal = await _context.WalletHolds
            .AsNoTracking()
            .Where(hold =>
                hold.OwnerType == WalletOwnerType.Driver &&
                hold.OwnerId == driverId &&
                hold.Status == WalletHoldStatus.Active)
            .SumAsync(hold => (decimal?)hold.Amount, cancellationToken) ?? 0m;

        var netWithdrawable = Math.Max(0m, walletBalance - codOwedBalance - pendingBalance - activeHoldTotal);

        // Finance summary
        var totalEarnings = wallet is not null
            ? await _context.WalletTransactions
                .Where(t => t.WalletId == wallet.Id && t.Direction == "IN")
                .SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0
            : 0;

        // Lifetime COD collected across delivered assignments (audit trail).
        var codCollectedLifetime = await _context.DeliveryAssignments
            .Where(a => a.DriverId == driverId && a.Status == AssignmentStatus.Delivered)
            .SumAsync(a => (decimal?)a.CodAmount, cancellationToken) ?? 0;

        var totalSettlements = await _context.Settlements
            .CountAsync(s =>
                (s.DriverId == driverId) ||
                (s.OwnerType == SettlementOwnerType.Driver && s.OwnerId == driverId),
                cancellationToken);

        var totalPayouts = await _context.Payouts
            .CountAsync(p =>
                (p.Settlement.DriverId == driverId) ||
                (p.Settlement.OwnerType == SettlementOwnerType.Driver && p.Settlement.OwnerId == driverId),
                cancellationToken);

        var recentSettlements = await _context.Settlements
            .AsNoTracking()
            .Where(s =>
                (s.DriverId == driverId) ||
                (s.OwnerType == SettlementOwnerType.Driver && s.OwnerId == driverId))
            .OrderByDescending(s => s.CreatedAtUtc)
            .Take(8)
            .Select(s => new AdminDriverFinanceSettlementSummaryDto(
                s.Id,
                s.Status.ToString(),
                s.GrossAmount,
                s.NetAmount,
                s.PeriodFrom,
                s.PeriodTo,
                s.CreatedAtUtc,
                s.ProcessedAtUtc))
            .ToArrayAsync(cancellationToken);

        var recentWithdrawals = await _context.DriverWithdrawalRequests
            .AsNoTracking()
            .Where(w => w.DriverId == driverId)
            .OrderByDescending(w => w.CreatedAtUtc)
            .Take(8)
            .Select(w => new AdminDriverFinanceWithdrawalSummaryDto(
                w.Id,
                w.Status.ToString(),
                w.Amount,
                w.RequestedPayoutDay.HasValue ? w.RequestedPayoutDay.Value.ToString() : null,
                w.TransferReference,
                w.CreatedAtUtc,
                w.ProcessedAtUtc,
                w.PayoutId))
            .ToArrayAsync(cancellationToken);

        var activeWithdrawals = recentWithdrawals
            .Where(w =>
                string.Equals(w.Status, DriverWithdrawalStatus.Pending.ToString(), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(w.Status, DriverWithdrawalStatus.Processing.ToString(), StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var activeWithdrawalsAmount = activeWithdrawals.Sum(w => w.Amount);

        // Notes
        var notes = await _context.DriverNotes
            .Include(n => n.Author)
            .Where(n => n.DriverId == driverId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(20)
            .Select(n => new AdminDriverNoteDto(n.Id, n.Author.FullName, n.Message, n.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);

        // Incidents
        var incidents = await _context.DriverIncidents
            .Where(i => i.DriverId == driverId)
            .OrderByDescending(i => i.CreatedAtUtc)
            .Take(20)
            .Select(i => new AdminDriverIncidentDto(
                i.Id, i.IncidentType, i.Severity.ToString(), i.Status.ToString(),
                i.ReviewerName, i.LinkedOrderId, i.Summary, i.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);

        var accountSupportCases = await _context.OrderSupportCases
            .AsNoTracking()
            .Include(c => c.Activities)
            .Where(c => c.DriverId == driverId && c.Type == OrderSupportCaseType.DriverAccountAppeal)
            .OrderByDescending(c => c.UpdatedAtUtc)
            .Take(10)
            .ToArrayAsync(cancellationToken);

        // Recent assignments
        var recentAssignmentRows = await _context.DeliveryAssignments
            .Where(a => a.DriverId == driverId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .Take(20)
            .Select(a => new
            {
                a.Id,
                a.OrderId,
                a.Order.OrderNumber,
                Status = a.Status.ToString(),
                a.AcceptedAtUtc,
                a.DeliveredAtUtc,
                a.FailedAtUtc,
                a.FailureReason,
                a.CodAmount,
                VendorName = a.Order.Vendor.BusinessNameEn,
                a.CreatedAtUtc
            })
            .ToArrayAsync(cancellationToken);

        var recentAssignments = recentAssignmentRows
            .Select(a => new AdminDriverAssignmentDto(
                a.Id, a.OrderId, a.OrderNumber, a.Status,
                a.AcceptedAtUtc, a.DeliveredAtUtc, a.FailedAtUtc, a.FailureReason, a.CodAmount))
            .ToArray();

        // Documents
        var documentApprovalOverlay = await ResolveDriverDocumentApprovalOverlayAsync(driver, cancellationToken);
        var vehicleApprovalOverlay = await ResolveDriverVehicleApprovalOverlayAsync(driver, cancellationToken);
        var effectiveProfile = ResolveEffectiveVehicleProfileFields(driver, vehicleApprovalOverlay);
        var documents = BuildAdminDriverDocuments(driver, documentApprovalOverlay, effectiveProfile);

        var commitmentSummary = await _driverCommitmentPolicyService.GetDriverSummaryAsync(driverId, cancellationToken);
        var allDriverIds = await _context.Drivers
            .AsNoTracking()
            .Select(d => d.Id)
            .ToArrayAsync(cancellationToken);
        var commitmentSummaries = await _driverCommitmentPolicyService.GetDriverSummariesAsync(allDriverIds, cancellationToken);

        var offerStats = await _context.DeliveryOfferAttempts
            .Where(a => a.DriverId == driverId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Accepted = g.Count(a => a.Status == DeliveryOfferAttemptStatus.Accepted),
                Rejected = g.Count(a => a.Status == DeliveryOfferAttemptStatus.Rejected),
                TimedOut = g.Count(a => a.Status == DeliveryOfferAttemptStatus.TimedOut)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var pendingPayoutDay = await _context.Payouts
            .AsNoTracking()
            .Where(p =>
                ((p.Settlement.DriverId == driverId) ||
                 (p.Settlement.OwnerType == SettlementOwnerType.Driver && p.Settlement.OwnerId == driverId)) &&
                (p.Status == PayoutStatus.Pending ||
                 p.Status == PayoutStatus.Processing ||
                 p.Status == PayoutStatus.Queued))
            .OrderBy(p => p.CreatedAtUtc)
            .Select(p => p.ScheduledPayoutDay)
            .FirstOrDefaultAsync(cancellationToken);

        var nextPayoutScheduleDay = pendingPayoutDay ?? driver.PayoutDay;
        var nextPayoutDateUtc = PayoutScheduleDayPolicy.NextOnOrAfter(DateTime.UtcNow.Date, nextPayoutScheduleDay);

        // Prefer primary, then any saved payout method (drivers may have methods without IsPrimary).
        var primaryPayoutMethod = await _context.DriverPayoutMethods
            .AsNoTracking()
            .Where(p => p.DriverId == driverId)
            .OrderByDescending(p => p.IsPrimary)
            .ThenByDescending(p => p.IsVerified)
            .ThenByDescending(p => p.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var statementPeriodFrom = recentSettlements.Length > 0
            ? recentSettlements.Min(s => s.PeriodFrom)
            : new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var statementPeriodTo = recentSettlements.Length > 0
            ? recentSettlements.Max(s => s.PeriodTo)
            : statementPeriodFrom.AddMonths(1).AddDays(-1);
        var statementPeriod = $"{statementPeriodFrom:yyyy-MM-dd} → {statementPeriodTo:yyyy-MM-dd}";

        var walletTransactions = wallet is null
            ? []
            : await _context.WalletTransactions
                .AsNoTracking()
                .Where(t => t.WalletId == wallet.Id)
                .OrderByDescending(t => t.CreatedAtUtc)
                .Take(20)
                .ToArrayAsync(cancellationToken);

        var regionDriverIds = !string.IsNullOrWhiteSpace(driver.City)
            ? await _context.Drivers
                .AsNoTracking()
                .Where(d => d.City == driver.City)
                .Select(d => d.Id)
                .ToArrayAsync(cancellationToken)
            : Array.Empty<Guid>();

        var regionAssignmentRows = regionDriverIds.Length == 0
            ? Array.Empty<AssignmentStatsRow>()
            : await _context.DeliveryAssignments
                .AsNoTracking()
                .Where(a => a.DriverId != null && regionDriverIds.Contains(a.DriverId.Value))
                .GroupBy(a => a.DriverId!.Value)
                .Select(g => new AssignmentStatsRow(
                    g.Key,
                    g.Count(),
                    g.Count(a => a.Status == AssignmentStatus.Delivered),
                    g.Count(a => a.Status == AssignmentStatus.Delivered ||
                        a.Status == AssignmentStatus.Failed ||
                        a.Status == AssignmentStatus.Cancelled ||
                        a.Status == AssignmentStatus.Returned)))
                .ToArrayAsync(cancellationToken);

        var fleetAssignmentRows = await _context.DeliveryAssignments
            .AsNoTracking()
            .Where(a => a.DriverId != null)
            .GroupBy(a => a.DriverId!.Value)
            .Select(g => new AssignmentStatsRow(
                g.Key,
                g.Count(),
                g.Count(a => a.Status == AssignmentStatus.Delivered),
                g.Count(a => a.Status == AssignmentStatus.Delivered ||
                    a.Status == AssignmentStatus.Failed ||
                    a.Status == AssignmentStatus.Cancelled ||
                    a.Status == AssignmentStatus.Returned)))
            .ToArrayAsync(cancellationToken);

        var activeDriversInCity = !string.IsNullOrWhiteSpace(driver.City)
            ? await _context.Drivers.CountAsync(
                d => d.City == driver.City &&
                     d.Status == AccountStatus.Active &&
                     d.IsAvailable,
                cancellationToken)
            : (int?)null;

        var avgDeliveryMinutes = await _context.DeliveryAssignments
            .AsNoTracking()
            .Where(a => a.DriverId == driverId &&
                a.AcceptedAtUtc.HasValue &&
                a.DeliveredAtUtc.HasValue)
            .Select(a => new
            {
                a.AcceptedAtUtc,
                a.DeliveredAtUtc
            })
            .ToArrayAsync(cancellationToken);

        var averageDeliveryMinutes = avgDeliveryMinutes.Length == 0
            ? (decimal?)null
            : Math.Round((decimal)avgDeliveryMinutes
                .Average(a => (a.DeliveredAtUtc!.Value - a.AcceptedAtUtc!.Value).TotalMinutes), 1);

        var workflowState = ResolveAdminWorkflowState(
            driver,
            activeTasks,
            wallet?.PendingBalance ?? 0,
            incidents,
            missingRequirements,
            commitmentSummary,
            driver.User.IsLoginLocked);

        var workflow = BuildAdminWorkflowSection(workflowState);
        var overview = new AdminDriverOverviewSectionDto(
            driver.Address,
            effectiveProfile.Region,
            effectiveProfile.City,
            effectiveProfile.LicenseNumber,
            Math.Round(completionRate, 0),
            commitmentSummary.CommitmentScore,
            ResolveCollectionPaymentStatus(codOwedBalance, codBlockThreshold));
        var operations = new AdminDriverOperationsSectionDto(
            effectiveProfile.Region,
            effectiveProfile.City,
            lastLocation?.Latitude,
            lastLocation?.Longitude,
            lastLocation?.AccuracyMeters,
            lastLocation?.RecordedAtUtc,
            driver.IsLocationUpdatesBlocked,
            driver.LocationUpdatesBlockReason,
            driver.LocationUpdatesBlockedAtUtc,
            activeDriversInCity,
            averageDeliveryMinutes,
            null,
            recentAssignmentRows.Select(a => new AdminDriverOperationTaskDto(
                a.Id,
                string.IsNullOrWhiteSpace(a.VendorName) ? a.OrderNumber : a.VendorName,
                driver.City ?? driver.User.FullName,
                a.Status,
                a.AcceptedAtUtc ?? a.CreatedAtUtc,
                ResolveDurationMinutes(a.AcceptedAtUtc, a.DeliveredAtUtc, a.FailedAtUtc),
                a.FailureReason,
                a.CodAmount)).ToArray());
        var support = BuildAdminSupportSection(
            notes,
            incidents,
            accountSupportCases,
            missingRequirements,
            wallet?.PendingBalance ?? 0);
        var documentHealth = BuildDocumentHealth(documents);
        var compliance = new AdminDriverComplianceSectionDto(
            incidents.Count(i => !string.Equals(i.Status, DriverIncidentStatus.Resolved.ToString(), StringComparison.OrdinalIgnoreCase)),
            incidents.Count(i => string.Equals(i.Severity, DriverIncidentSeverity.Critical.ToString(), StringComparison.OrdinalIgnoreCase)),
            incidents.Count(i => !string.Equals(i.Severity, DriverIncidentSeverity.Medium.ToString(), StringComparison.OrdinalIgnoreCase)),
            documents.Count(d => !string.Equals(d.Status, "valid", StringComparison.OrdinalIgnoreCase)),
            driver.Status is AccountStatus.Suspended or AccountStatus.Banned ? 1 : 0,
            ResolveRiskLevel(driver, incidents, wallet?.PendingBalance ?? 0, missingRequirements),
            documentHealth);
        var financeDetails = new AdminDriverFinanceSectionDto(
            AvailableBalance: netWithdrawable,
            DueAmount: codOwedBalance,
            CodCollected: codCollectedLifetime,
            PendingDeductions: Math.Max(0m, pendingBalance + activeHoldTotal),
            NextPayoutDateUtc: nextPayoutDateUtc,
            PayoutMethod: primaryPayoutMethod?.MethodType.ToString(),
            StatementPeriod: statementPeriod,
            Entries: walletTransactions.Select(MapFinanceEntry).ToArray(),
            CurrentBalance: walletBalance,
            PendingBalance: pendingBalance,
            CodOwedBalance: codOwedBalance,
            CodBlockThresholdAmount: codBlockThreshold,
            NetWithdrawable: netWithdrawable,
            PayoutDay: driver.PayoutDay.ToString(),
            ActiveWithdrawalsCount: activeWithdrawals.Length,
            ActiveWithdrawalsAmount: activeWithdrawalsAmount,
            SettlementsCount: totalSettlements,
            PayoutsCount: totalPayouts,
            RecentSettlements: recentSettlements,
            RecentWithdrawals: recentWithdrawals,
            PayoutMethodLabel: primaryPayoutMethod?.MaskedLabel);
        var performanceDetails = BuildAdminPerformanceSection(
            Math.Round(completionRate, 0),
            Math.Round(acceptanceRate, 0),
            commitmentSummary.CommitmentScore,
            completedTasks,
            offerStats?.Rejected ?? 0,
            offerStats?.TimedOut ?? 0,
            regionAssignmentRows,
            fleetAssignmentRows,
            regionDriverIds,
            commitmentSummaries,
            incidents,
            wallet?.PendingBalance ?? 0);
        var profileReadiness = DriverProfileReadinessFactory.BuildAdminReadiness(
            driver,
            driver.User,
            new DriverProfileReadinessFactory.FieldOverlay(
                effectiveProfile.NationalId,
                effectiveProfile.LicenseNumber,
                effectiveProfile.VehicleLicenseNumber,
                effectiveProfile.Region,
                effectiveProfile.City));
        string? reviewerName = null;
        if (driver.ReviewedByUserId is Guid reviewerId)
        {
            reviewerName = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == reviewerId)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var verification = BuildAdminVerificationSection(
            driver,
            profileReadiness.Checklist,
            profileReadiness.MissingRequirements,
            Math.Round(completionRate, 0),
            Math.Round(acceptanceRate, 0),
            reviewerName);

        return new AdminDriverDetailDto(
            Id: driver.Id,
            DriverDisplayId: FormatDriverDisplayId(driver.Id),
            FirstName: driver.User.FullName.Split(' ').FirstOrDefault() ?? driver.User.FullName,
            LastName: string.Join(' ', driver.User.FullName.Split(' ').Skip(1)),
            PhoneNumber: driver.User.PhoneNumber ?? "",
            Email: driver.User.Email ?? "",
            ImageUrl: driver.PersonalPhotoUrl,
            City: effectiveProfile.City ?? "",
            Status: MapDriverStatus(driver, activeTasks),
            VerificationStatus: driver.VerificationStatus.ToString(),
            VehicleType: effectiveProfile.VehicleType,
            JoinedAt: driver.CreatedAtUtc,
            LastSeenAt: lastLocation?.RecordedAtUtc ?? driver.UpdatedAtUtc,
            ActiveTasks: activeTasks,
            CompletedTasks: completedTasks,
            AcceptanceRate: Math.Round(acceptanceRate, 0),
            WalletBalance: walletBalance,
            Performance: DerivePerformance(acceptanceRate),
            Issues: DeriveIssues(driver, walletBalance, commitmentSummary),
            CollectionPaymentStatus: ResolveCollectionPaymentStatus(codOwedBalance, codBlockThreshold),
            Alerts: null,
            CommitmentScore: commitmentSummary.CommitmentScore,
            DailyRejections: commitmentSummary.DailyRejections,
            WeeklyRejections: commitmentSummary.WeeklyRejections,
            EnforcementLevel: commitmentSummary.EnforcementLevel,
            LastOfferResponseAtUtc: commitmentSummary.LastOfferResponseAtUtc,
            Address: driver.Address,
            LicenseNumber: effectiveProfile.LicenseNumber,
            NationalId: effectiveProfile.NationalId,
            NationalIdExpiryDate: effectiveProfile.NationalIdExpiryDate,
            DriverLicenseExpiryDate: effectiveProfile.DriverLicenseExpiryDate,
            VehicleLicenseNumber: effectiveProfile.VehicleLicenseNumber,
            VehicleLicenseExpiryDate: effectiveProfile.VehicleLicenseExpiryDate,

            ReviewedAtUtc: driver.ReviewedAtUtc,
            ReviewNote: driver.ReviewNote,
            SuspensionReason: driver.SuspensionReason,
            IsLoginLocked: driver.User.IsLoginLocked,
            LockedAtUtc: driver.User.LockedAtUtc,
            LockReason: driver.User.LockReason,
            ProfileReadiness: profileReadiness,
            Documents: documents,
            Notes: notes,
            Incidents: incidents,
            Finance: new AdminDriverFinanceSummaryDto(
                walletBalance, pendingBalance,
                totalEarnings, codCollectedLifetime, totalSettlements, totalPayouts),
            RecentAssignments: recentAssignments,
            Overview: overview,
            Workflow: workflow,
            Operations: operations,
            PerformanceDetails: performanceDetails,
            Support: support,
            Compliance: compliance,
            FinanceDetails: financeDetails,
            Verification: verification);
    }

    public async Task<AdminDriverFinanceEntriesListDto?> GetAdminDriverFinanceEntriesAsync(
        Guid driverId,
        int page,
        int pageSize,
        string? status = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var driverExists = await _context.Drivers
            .AsNoTracking()
            .AnyAsync(d => d.Id == driverId, cancellationToken);

        if (!driverExists)
        {
            return null;
        }

        var wallet = await _context.Wallets
            .AsNoTracking()
            .FirstOrDefaultAsync(
                w => w.OwnerType == WalletOwnerType.Driver && w.OwnerId == driverId,
                cancellationToken);

        if (wallet is null)
        {
            return new AdminDriverFinanceEntriesListDto([], 0, page, pageSize);
        }

        var query = _context.WalletTransactions
            .AsNoTracking()
            .Where(t => t.WalletId == wallet.Id);

        var normalizedSearch = search?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            var searchLower = normalizedSearch.ToLowerInvariant();
            query = query.Where(t =>
                t.TxnType.ToString().ToLower().Contains(searchLower) ||
                t.Id.ToString().ToLower().Contains(searchLower) ||
                (t.OrderId != null && t.OrderId.Value.ToString().ToLower().Contains(searchLower)) ||
                (t.ReferenceId != null && t.ReferenceId.Value.ToString().ToLower().Contains(searchLower)) ||
                (t.SettlementId != null && t.SettlementId.Value.ToString().ToLower().Contains(searchLower)));
        }

        var normalizedStatus = status?.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(normalizedStatus) && normalizedStatus != "ALL")
        {
            query = normalizedStatus switch
            {
                "SETTLED" => query.Where(t =>
                    t.SettlementId != null ||
                    t.TxnType == WalletTxnType.Settlement ||
                    t.TxnType == WalletTxnType.Payout),
                "PENDING" => query.Where(t => t.TxnType == WalletTxnType.Hold),
                "POSTED" => query.Where(t =>
                    t.SettlementId == null &&
                    t.TxnType != WalletTxnType.Hold &&
                    t.TxnType != WalletTxnType.Settlement &&
                    t.TxnType != WalletTxnType.Payout),
                // Wallet ledger has no failed txn state; keep empty instead of inventing rows.
                "FAILED" => query.Where(_ => false),
                _ => query
            };
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(t => t.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);

        return new AdminDriverFinanceEntriesListDto(
            items.Select(MapFinanceEntry).ToArray(),
            totalCount,
            page,
            pageSize);
    }

    public async Task<DriverAssignmentDetailDto?> GetAssignmentDetailAsync(
        Guid driverId,
        Guid assignmentId,
        CancellationToken cancellationToken = default)
    {
        var assignment = await _context.DeliveryAssignments
            .AsNoTracking()
            .Include(a => a.Order)
                .ThenInclude(o => o.Vendor)
            .Include(a => a.Order)
                .ThenInclude(o => o.VendorBranch)
            .Include(a => a.Order)
                .ThenInclude(o => o.Items)
                    .ThenInclude(i => i.MasterProduct)
            .Include(a => a.Driver)
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.DriverId == driverId, cancellationToken);

        if (assignment is null)
        {
            return null;
        }

        if (DeliveryActiveAssignmentRules.IsTerminalOrder(assignment.Order.Status))
        {
            return null;
        }

        var customerAddress = await _context.CustomerAddresses
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == assignment.Order.CustomerAddressId, cancellationToken);

        var assignmentStatus = assignment.Status.ToString();
        var homeState = ResolveAssignmentHomeState(assignment);
        var otpPickupStatus = ResolveOtpStatus(assignment.RequiresPickupOtpVerification, assignment.IsPickupOtpVerified);
        var otpDeliveryStatus = ResolveOtpStatus(assignment.RequiresDeliveryOtpVerification, assignment.IsDeliveryOtpVerified);
        var arrivalState = ResolveArrivalState(assignment);
        var paymentMethod = assignment.Order.PaymentMethod.ToString();

        return new DriverAssignmentDetailDto(
            assignment.Id,
            assignment.OrderId,
            assignment.Order.OrderNumber,
            assignmentStatus,
            ResolveAssignmentStatusLabel(assignmentStatus),
            homeState,
            ResolveHomeStateLabel(homeState),
            ResolveAllowedActions(assignment, assignment.Order.Status),
            IsArabic()
                ? (assignment.Order.Vendor.BusinessNameAr ?? assignment.Order.Vendor.BusinessNameEn)
                : (assignment.Order.Vendor.BusinessNameEn ?? assignment.Order.Vendor.BusinessNameAr),
            assignment.Order.Vendor.LogoUrl,
            assignment.Order.VendorBranch?.AddressLine ?? assignment.Order.Vendor.NationalAddress ?? string.Empty,
            assignment.Order.VendorBranch?.Latitude,
            assignment.Order.VendorBranch?.Longitude,
            assignment.Order.Vendor.ContactPhone,
            customerAddress?.ContactName ?? "Customer",
            BuildFullCustomerAddress(customerAddress),
            customerAddress?.Latitude,
            customerAddress?.Longitude,
            customerAddress?.ContactPhone,
            paymentMethod,
            ResolvePaymentMethodLabel(paymentMethod),
            ResolveCodAmount(assignment),
            assignment.RequiresPickupOtpVerification,
            otpPickupStatus,
            ResolveOtpStatusLabel(otpPickupStatus),
            assignment.RequiresDeliveryOtpVerification,
            otpDeliveryStatus,
            ResolveOtpStatusLabel(otpDeliveryStatus),
            assignment.IsInHandoffWindow ? assignment.PickupOtpCode : null,
            arrivalState,
            ResolveArrivalStateLabel(arrivalState),
            assignment.Order.Items
                .Select(item => new DriverAssignmentItemDto(
                    ResolveItemName(item),
                    item.SnapshotImageUrl,
                    item.Quantity,
                    item.UnitPrice,
                    item.LineTotal,
                    item.SnapshotDisplaySize,
                    item.UnitName,
                    IsArabic()
                        ? (assignment.Order.Vendor.BusinessNameAr ?? assignment.Order.Vendor.BusinessNameEn)
                        : (assignment.Order.Vendor.BusinessNameEn ?? assignment.Order.Vendor.BusinessNameAr)))
                .ToArray());
    }

    public async Task<DriverCompletedOrdersListDto> GetCompletedOrdersAsync(
        Guid driverId,
        string? status = null,
        int page = 1,
        int perPage = 20,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = page <= 0 ? 1 : page;
        var normalizedPerPage = perPage <= 0 ? 20 : Math.Clamp(perPage, 1, 100);
        var normalizedStatus = string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToLowerInvariant();
        var query = _context.DeliveryAssignments
            .AsNoTracking()
            .Include(a => a.Order)
                .ThenInclude(o => o.Vendor)
            .Include(a => a.Order)
                .ThenInclude(o => o.Items)
                    .ThenInclude(i => i.MasterProduct)
            .Where(a => a.DriverId == driverId &&
                (a.Order.Status == OrderStatus.Delivered ||
                 a.Order.Status == OrderStatus.Cancelled ||
                 a.Order.Status == OrderStatus.DeliveryFailed));

        if (normalizedStatus is not null)
        {
            query = normalizedStatus switch
            {
                "delivered" => query.Where(a => a.Order.Status == OrderStatus.Delivered),
                "cancelled" => query.Where(a => a.Order.Status == OrderStatus.Cancelled),
                "deliveryfailed" or "delivery_failed" => query.Where(a => a.Order.Status == OrderStatus.DeliveryFailed),
                _ => query.Where(_ => false)
            };
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var assignments = await query
            .OrderByDescending(a => a.DeliveredAtUtc ?? a.FailedAtUtc ?? a.Order.CancelledAtUtc ?? a.UpdatedAtUtc)
            .Skip((normalizedPage - 1) * normalizedPerPage)
            .Take(normalizedPerPage)
            .ToListAsync(cancellationToken);

        var addressIds = assignments
            .Select(a => a.Order.CustomerAddressId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();
        var addresses = await _context.CustomerAddresses
            .AsNoTracking()
            .Where(a => addressIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        var items = assignments
            .Select(assignment =>
            {
                CustomerAddress? customerAddress = null;
                if (assignment.Order.CustomerAddressId.HasValue)
                {
                    addresses.TryGetValue(assignment.Order.CustomerAddressId.Value, out customerAddress);
                }

                return new DriverCompletedOrderListItemDto(
                    assignment.OrderId,
                    IsArabic()
                        ? (assignment.Order.Vendor.BusinessNameAr ?? assignment.Order.Vendor.BusinessNameEn)
                        : (assignment.Order.Vendor.BusinessNameEn ?? assignment.Order.Vendor.BusinessNameAr),
                    assignment.Order.Vendor.LogoUrl,
                    customerAddress?.ContactName ?? "Customer",
                    ResolveCompletedAtUtc(assignment),
                    MapCompletedOrderStatus(assignment.Order.Status),
                    ResolveCodAmount(assignment),
                    ResolveDistanceKm(assignment.Order, customerAddress),
                    assignment.Order.PaymentMethod.ToString(),
                    BuildFullCustomerAddress(customerAddress),
                    assignment.Order.Items
                        .Select(item => new DriverCompletedOrderItemDto(ResolveItemName(item), item.SnapshotImageUrl, item.Quantity, item.UnitPrice, item.LineTotal))
                        .ToArray());
            })
            .ToArray();

        var hasMore = normalizedPage * normalizedPerPage < totalCount;

        return new DriverCompletedOrdersListDto(
            items,
            totalCount,
            normalizedPage,
            normalizedPerPage,
            hasMore);
    }

    public async Task<DriverCompletedOrderDetailDto?> GetCompletedOrderDetailAsync(
        Guid driverId,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var assignment = await _context.DeliveryAssignments
            .AsNoTracking()
            .Include(a => a.Order)
                .ThenInclude(o => o.Vendor)
            .Include(a => a.Order)
                .ThenInclude(o => o.VendorBranch)
            .Include(a => a.Order)
                .ThenInclude(o => o.Items)
                    .ThenInclude(i => i.MasterProduct)
            .Where(a => a.DriverId == driverId && a.OrderId == orderId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (assignment is null)
        {
            return null;
        }

        if (assignment.Order.Status is not (OrderStatus.Delivered or OrderStatus.Cancelled or OrderStatus.DeliveryFailed))
        {
            return null;
        }

        var customerAddress = await _context.CustomerAddresses
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == assignment.Order.CustomerAddressId, cancellationToken);

        return new DriverCompletedOrderDetailDto(
            assignment.OrderId,
            assignment.Id,
            assignment.Order.OrderNumber,
            IsArabic()
                ? (assignment.Order.Vendor.BusinessNameAr ?? assignment.Order.Vendor.BusinessNameEn)
                : (assignment.Order.Vendor.BusinessNameEn ?? assignment.Order.Vendor.BusinessNameAr),
            assignment.Order.Vendor.LogoUrl,
            assignment.Order.Vendor.ContactPhone,
            customerAddress?.ContactName ?? "Customer",
            customerAddress?.ContactPhone,
            assignment.Order.VendorBranch?.AddressLine ?? assignment.Order.Vendor.NationalAddress ?? string.Empty,
            BuildFullCustomerAddress(customerAddress),
            MapCompletedOrderStatus(assignment.Order.Status),
            assignment.Order.PaymentMethod.ToString(),
            ResolveCodAmount(assignment),
            assignment.Order.DeliveryFee,
            ResolveDistanceKm(assignment.Order, customerAddress),
            ResolveCompletedAtUtc(assignment),
            assignment.Order.Items
                .Select(item => new DriverCompletedOrderItemDto(ResolveItemName(item), item.SnapshotImageUrl, item.Quantity, item.UnitPrice, item.LineTotal))
                .ToArray());
    }

    public async Task<DriverProfileDto?> GetDriverProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var driver = await _context.Drivers
            .Include(d => d.User)
            .Include(d => d.DocumentReviews)
            .FirstOrDefaultAsync(d => d.UserId == userId, cancellationToken);

        if (driver is null)
        {
            return null;
        }

        if (driver.ApplyDocumentExpiryLock())
        {
            await _context.SaveChangesAsync(cancellationToken);
            await DriverExpiryLockNotificationDispatcher.NotifyAsync(
                driver,
                _notificationService,
                _oneSignalPushService,
                cancellationToken);
        }

        var missingRequirements = DriverProfileReadinessFactory.GetMissingRequirements(driver, driver.User);
        var completionPercent = DriverProfileReadinessFactory.GetCompletionPercent(missingRequirements.Count);
        var commitmentSummary = await _driverCommitmentPolicyService.GetDriverSummaryAsync(driver.Id, cancellationToken);
        var dailyLimit = DriverCommitmentPolicyService.GetDailyRejectionLimit();
        var weeklyLimit = DriverCommitmentPolicyService.GetWeeklyRejectionLimit();

        // Resolve geography display names
        string? regionNameAr = null, regionNameEn = null, cityNameAr = null, cityNameEn = null;
        if (!string.IsNullOrWhiteSpace(driver.Region))
        {
            var regionEntity = await _context.SaudiRegions
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Code == driver.Region, cancellationToken);
            regionNameAr = regionEntity?.NameAr;
            regionNameEn = regionEntity?.NameEn;

            if (!string.IsNullOrWhiteSpace(driver.City) && regionEntity is not null)
            {
                var cityEntity = await _context.SaudiCities
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Code == driver.City && c.RegionId == regionEntity.Id, cancellationToken);
                cityNameAr = cityEntity?.NameAr;
                cityNameEn = cityEntity?.NameEn;
            }
        }

        var documentApprovalOverlay = await ResolveDriverDocumentApprovalOverlayAsync(driver, cancellationToken);
        var sections = await BuildDriverProfileSectionsAsync(driver, documentApprovalOverlay, cancellationToken);

        return new DriverProfileDto(
            driver.User.FullName,
            driver.User.Email ?? string.Empty,
            driver.User.PhoneNumber ?? string.Empty,
            driver.Address,
            driver.VehicleType?.ToString(),
            driver.LicenseNumber,
            driver.NationalIdExpiryDate,
            driver.DriverLicenseExpiryDate,
            driver.VehicleLicenseNumber,
            driver.VehicleLicenseExpiryDate,
            driver.NationalId,
            driver.PersonalPhotoUrl,
            driver.NationalIdFrontImageUrl,
            driver.NationalIdBackImageUrl,
            driver.LicenseImageUrl,
            driver.VehicleImageUrl,
            BuildDriverProfileDocuments(driver, documentApprovalOverlay),
            sections,
            driver.Region,
            driver.City,
            regionNameAr,
            regionNameEn,
            cityNameAr,
            cityNameEn,
            driver.VerificationStatus.ToString(),
            driver.Status.ToString(),
            driver.ReviewNote,
            driver.SuspensionReason,
            new DriverRejectionPolicyDto(
                commitmentSummary.DailyRejections,
                dailyLimit,
                Math.Max(0, dailyLimit - commitmentSummary.DailyRejections),
                !commitmentSummary.CanReceiveOffers,
                commitmentSummary.RestrictionMessage,
                commitmentSummary.WeeklyRejections,
                weeklyLimit,
                Math.Max(0, weeklyLimit - commitmentSummary.WeeklyRejections),
                commitmentSummary.RestrictionMessage,
                commitmentSummary.RestrictionMessageEn),
            missingRequirements.Count == 0,
            completionPercent,
            missingRequirements,
            missingRequirements.Count == 0,
            DriverOperationalStatusFactory.ResolveReviewNoteAr(driver.ReviewNote),
            DriverOperationalStatusFactory.ResolveReviewNoteEn(driver.ReviewNote));
    }

    public async Task<DeliveryZoneDto[]> GetActiveZonesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.DeliveryZones
            .Where(z => z.IsActive)
            .OrderBy(z => z.City).ThenBy(z => z.Name)
            .Select(z => new DeliveryZoneDto(z.Id, z.City, z.Name, z.CenterLat, z.CenterLng, z.RadiusKm, z.IsActive))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<DeliveryZoneDto[]> GetAllZonesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.DeliveryZones
            .OrderBy(z => z.City).ThenBy(z => z.Name)
            .Select(z => new DeliveryZoneDto(z.Id, z.City, z.Name, z.CenterLat, z.CenterLng, z.RadiusKm, z.IsActive))
            .ToArrayAsync(cancellationToken);
    }

    private static string MapDriverStatus(Driver d, int activeTasks)
    {
        if (d.HasExpiredRequiredDocuments()) return "Inactive";
        if (d.Status == AccountStatus.Banned) return "Banned";
        if (d.Status == AccountStatus.Suspended) return "Suspended";
        if (d.Status == AccountStatus.Inactive) return "Inactive";
        if (activeTasks > 0) return "OnMission";
        if (!d.IsAvailable) return "Offline";
        return "Online";
    }

    private static string DerivePerformance(decimal acceptanceRate) =>
        acceptanceRate >= 90 ? "Excellent" :
        acceptanceRate >= 75 ? "Good" :
        acceptanceRate >= 55 ? "NeedsImprovement" : "Low";

    private static bool TryParseVehicleType(string value, out DriverVehicleType vehicleType) =>
        DriverVehicleTypeMapper.TryParse(value, out vehicleType);

    private static string[] DeriveIssues(
        Driver driver,
        decimal walletBalance,
        DriverCommitmentSummaryDto commitmentSummary)
    {
        var issues = new List<string>();
        if (driver.VerificationStatus is DriverVerificationStatus.NeedsDocuments or DriverVerificationStatus.UnderReview)
            issues.Add("warning");
        if (driver.HasExpiredRequiredDocuments())
            issues.Add("compliance");
        if (walletBalance < 0)
            issues.Add("payment");
        if (driver.Status is AccountStatus.Suspended or AccountStatus.Banned)
            issues.Add("legal");
        if (driver.IsLocationUpdatesBlocked)
            issues.Add("location");
        if (!commitmentSummary.CanReceiveOffers)
            issues.Add("dispatch");
        return issues.Count > 0 ? issues.ToArray() : ["clear"];
    }

    private static string ResolveAssignmentHomeState(DeliveryAssignment assignment) =>
        assignment.Status == AssignmentStatus.OfferSent ? "IncomingOffer" : "OnMission";

    private static string ResolveAssignmentStatusLabel(string status) => status switch
    {
        "SearchingDriver" => IsArabic() ? "جاري البحث عن مندوب" : "Searching for driver",
        "OfferSent"       => IsArabic() ? "عرض مرسل" : "Offer sent",
        "Accepted"        => IsArabic() ? "مقبول" : "Accepted",
        "ArrivedAtVendor" => IsArabic() ? "وصل للمتجر" : "Arrived at vendor",
        "PickedUp"        => IsArabic() ? "استلمنا" : "Picked up",
        "ArrivedAtCustomer" => IsArabic() ? "وصل للعميل" : "Arrived at customer",
        "Delivered"       => IsArabic() ? "وصلنا" : "Delivered",
        "Failed"          => IsArabic() ? "فشل التوصيل" : "Delivery failed",
        "Cancelled"       => IsArabic() ? "ملغي" : "Cancelled",
        "Rejected"        => IsArabic() ? "مرفوض" : "Rejected",
        _                 => status
    };

    private static string ResolveHomeStateLabel(string homeState) => homeState switch
    {
        "IncomingOffer" => IsArabic() ? "عرض جديد" : "Incoming offer",
        "OnMission"     => IsArabic() ? "في مهمة" : "On mission",
        _               => homeState
    };

    private static string ResolveOtpStatusLabel(string otpStatus) => otpStatus switch
    {
        "not_required" => IsArabic() ? "غير مطلوب" : "Not required",
        "pending"      => IsArabic() ? "في الانتظار" : "Pending",
        "verified"     => IsArabic() ? "تحققنا" : "Verified",
        _              => otpStatus
    };

    private static string ResolveArrivalStateLabel(string arrivalState) => arrivalState switch
    {
        "en_route"           => IsArabic() ? "في الطريق" : "En route",
        "arrived_at_vendor"  => IsArabic() ? "وصل للمتجر" : "Arrived at vendor",
        "arrived_at_customer" => IsArabic() ? "وصل للعميل" : "Arrived at customer",
        _                    => arrivalState
    };

    private static string ResolvePaymentMethodLabel(string paymentMethod) => paymentMethod switch
    {
        "CashOnDelivery" => IsArabic() ? "الدفع عند الاستلام" : "Cash on delivery",
        "Online"         => IsArabic() ? "دفع إلكتروني" : "Online payment",
        "Wallet"         => IsArabic() ? "المحفظة" : "Wallet",
        _                => paymentMethod
    };

    private static bool IsArabic() =>
        System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
            .Equals("ar", StringComparison.OrdinalIgnoreCase);

    private static string ResolveItemName(Domain.Modules.Orders.Entities.OrderItem item)
    {
        if (item.MasterProduct is null)
        {
            return item.ProductName;
        }

        var preferred = IsArabic() ? item.MasterProduct.NameAr : item.MasterProduct.NameEn;
        var fallback = IsArabic() ? item.MasterProduct.NameEn : item.MasterProduct.NameAr;
        return preferred?.Trim() ?? fallback?.Trim() ?? item.ProductName;
    }

    private static string BuildFullCustomerAddress(Domain.Modules.Identity.Entities.CustomerAddress? address)
    {
        if (address is null)
        {
            return string.Empty;
        }

        var parts = new List<string>();

        AddAddressPart(parts, address.AddressLine);

        if (!string.IsNullOrWhiteSpace(address.BuildingNo))
            parts.Add(IsArabic() ? $"مبنى {address.BuildingNo.Trim()}" : $"Bldg {address.BuildingNo.Trim()}");

        if (!string.IsNullOrWhiteSpace(address.FloorNo))
            parts.Add(IsArabic() ? $"طابق {address.FloorNo.Trim()}" : $"Floor {address.FloorNo.Trim()}");

        if (!string.IsNullOrWhiteSpace(address.ApartmentNo))
            parts.Add(IsArabic() ? $"شقة {address.ApartmentNo.Trim()}" : $"Apt {address.ApartmentNo.Trim()}");

        AddAddressPart(parts, address.Area);

        AddAddressPart(parts, address.City);

        return parts.Count > 0 ? string.Join("، ", parts) : string.Empty;
    }

    private static void AddAddressPart(ICollection<string> parts, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var candidate = value.Trim();
        var normalizedCandidate = NormalizeAddressPart(candidate);
        if (string.IsNullOrWhiteSpace(normalizedCandidate))
        {
            return;
        }

        var alreadyIncluded = parts.Any(part =>
        {
            var normalizedPart = NormalizeAddressPart(part);
            return normalizedPart.Contains(normalizedCandidate, StringComparison.OrdinalIgnoreCase) ||
                   normalizedCandidate.Contains(normalizedPart, StringComparison.OrdinalIgnoreCase);
        });

        if (!alreadyIncluded)
        {
            parts.Add(candidate);
        }
    }

    private static string NormalizeAddressPart(string value) =>
        new(value
            .Trim()
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());

    private static IReadOnlyList<string> ResolveAllowedActions(DeliveryAssignment assignment, OrderStatus orderStatus)
    {
        if (assignment.Status == AssignmentStatus.OfferSent)
        {
            return ["accept_offer", "reject_offer"];
        }

        if (assignment.Status == AssignmentStatus.Accepted)
        {
            return ["arrived_at_vendor"];
        }

        if (assignment.Status == AssignmentStatus.ArrivedAtVendor)
        {
            if (assignment.IsPickupOtpVerified)
            {
                return ["mark_picked_up"];
            }

            return assignment.RequiresPickupOtpVerification
                ? ["verify_pickup_otp"]
                : ["mark_picked_up"];
        }

        if (assignment.Status == AssignmentStatus.PickedUp && orderStatus != OrderStatus.OnTheWay)
        {
            return ["mark_on_the_way"];
        }

        if (assignment.Status == AssignmentStatus.PickedUp && orderStatus == OrderStatus.OnTheWay)
        {
            return ["arrived_at_customer"];
        }

        if (assignment.Status == AssignmentStatus.ArrivedAtCustomer)
        {
            return assignment.RequiresDeliveryOtpVerification
                ? ["verify_delivery_otp"]
                : [];
        }

        return Array.Empty<string>();
    }

    private static string ResolveOtpStatus(bool required, bool verified)
    {
        if (!required && !verified)
        {
            return "not_required";
        }

        return verified ? "verified" : "pending";
    }

    private static string ResolveArrivalState(DeliveryAssignment assignment)
    {
        if (assignment.Status == AssignmentStatus.ArrivedAtCustomer)
        {
            return "arrived_at_customer";
        }

        if (assignment.Status == AssignmentStatus.ArrivedAtVendor)
        {
            return "arrived_at_vendor";
        }

        if (assignment.Status == AssignmentStatus.PickedUp)
        {
            return "en_route";
        }

        if (assignment.ArrivedAtCustomerAtUtc.HasValue)
        {
            return "arrived_at_customer";
        }

        // Historical vendor-arrival timestamps should not keep the driver stuck in the
        // handoff step after pickup has already been confirmed.
        if (assignment.ArrivedAtVendorAtUtc.HasValue && !assignment.PickedUpAtUtc.HasValue)
        {
            return "arrived_at_vendor";
        }

        return "en_route";
    }

    private static DateTime? ResolveCompletedAtUtc(DeliveryAssignment assignment) =>
        assignment.DeliveredAtUtc
        ?? assignment.FailedAtUtc
        ?? assignment.Order.CancelledAtUtc
        ?? assignment.Order.DeliveredAtUtc;

    private static string MapCompletedOrderStatus(OrderStatus status) =>
        status switch
        {
            OrderStatus.Delivered => "delivered",
            OrderStatus.Cancelled => "cancelled",
            OrderStatus.DeliveryFailed => "deliveryFailed",
            _ => status.ToString()
        };

    private static decimal ResolveCodAmount(DeliveryAssignment assignment) =>
        assignment.Order.PaymentMethod == PaymentMethodType.CashOnDelivery ? assignment.Order.TotalAmount : 0m;

    private static decimal ResolveDistanceKm(Order order, CustomerAddress? customerAddress)
    {
        if (order.QuotedDistanceKm.HasValue)
        {
            return Math.Round(order.QuotedDistanceKm.Value, 2);
        }

        if (order.VendorBranch is null ||
            customerAddress?.Latitude is null ||
            customerAddress.Longitude is null)
        {
            return 0m;
        }

        return Math.Round(ApproximateDistanceKm(
            order.VendorBranch.Latitude,
            order.VendorBranch.Longitude,
            customerAddress.Latitude.Value,
            customerAddress.Longitude.Value), 2);
    }

    private static decimal ApproximateDistanceKm(decimal lat1, decimal lng1, decimal lat2, decimal lng2)
    {
        var dLat = (double)(lat2 - lat1) * Math.PI / 180;
        var dLng = (double)(lng2 - lng1) * Math.PI / 180;
        var avgLat = (double)(lat1 + lat2) / 2 * Math.PI / 180;
        var x = dLng * Math.Cos(avgLat);
        var y = dLat;
        return (decimal)(Math.Sqrt(x * x + y * y) * 6371);
    }

    private static AdminDriverWorkflowSectionDto BuildAdminWorkflowSection(string state)
    {
        var readiness = state switch
        {
            "READY_FOR_DISPATCH" or "ACTIVE_DELIVERY" => "READY",
            "FINANCE_HOLD" or "READY_TO_ACTIVATE" => "LIMITED",
            "OFFER_RESTRICTED" or "LOCATION_RESTRICTED" or "LOGIN_LOCKED" => "BLOCKED",
            _ => "BLOCKED"
        };

        var blockers = state switch
        {
            "BANNED" => ["account_banned"],
            "SUSPENDED" => ["account_suspended"],
            "PENDING_DOCUMENTS" => ["missing_documents"],
            "VERIFICATION_REVIEW" => ["verification_in_progress"],
            "COMPLIANCE_REVIEW" => ["open_compliance_case"],
            "FINANCE_HOLD" => ["finance_hold"],
            "OFFER_RESTRICTED" => ["offer_restricted"],
            "LOCATION_RESTRICTED" => ["location_updates_blocked"],
            "LOGIN_LOCKED" => ["login_locked"],
            _ => Array.Empty<string>()
        };

        var alerts = state switch
        {
            "ACTIVE_DELIVERY" => ["driver_on_active_mission"],
            "READY_FOR_DISPATCH" => ["ready_for_dispatch"],
            "READY_TO_ACTIVATE" => ["driver_offline_but_approved"],
            "OFFER_RESTRICTED" => ["offer_restriction_active"],
            "LOCATION_RESTRICTED" => ["location_updates_blocked"],
            "LOGIN_LOCKED" => ["login_locked"],
            _ => Array.Empty<string>()
        };

        var actions = state switch
        {
            "BANNED" => new[]
            {
                new AdminDriverWorkflowActionDto("UNBAN_DRIVER", "success", "overview"),
                new AdminDriverWorkflowActionDto("REVIEW_COMPLIANCE", "warning", "compliance"),
                new AdminDriverWorkflowActionDto("OPEN_SUPPORT", "secondary", "support")
            },
            "SUSPENDED" => new[]
            {
                new AdminDriverWorkflowActionDto("REVIEW_COMPLIANCE", "warning", "compliance"),
                new AdminDriverWorkflowActionDto("OPEN_FINANCE", "secondary", "finance"),
                new AdminDriverWorkflowActionDto("REACTIVATE_DRIVER", "success", "overview"),
                new AdminDriverWorkflowActionDto("BAN_DRIVER", "danger", "overview")
            },
            "PENDING_DOCUMENTS" => new[]
            {
                new AdminDriverWorkflowActionDto("REQUEST_DOCUMENTS", "warning", "verification"),
                new AdminDriverWorkflowActionDto("OPEN_SUPPORT", "secondary", "support")
            },
            "VERIFICATION_REVIEW" => new[]
            {
                new AdminDriverWorkflowActionDto("APPROVE_VERIFICATION", "success", "verification"),
                new AdminDriverWorkflowActionDto("REQUEST_DOCUMENTS", "warning", "verification"),
                new AdminDriverWorkflowActionDto("REJECT_VERIFICATION", "danger", "verification")
            },
            "COMPLIANCE_REVIEW" => new[]
            {
                new AdminDriverWorkflowActionDto("REVIEW_COMPLIANCE", "warning", "compliance"),
                new AdminDriverWorkflowActionDto("SUSPEND_DRIVER", "danger", "overview"),
                new AdminDriverWorkflowActionDto("BAN_DRIVER", "danger", "overview"),
                new AdminDriverWorkflowActionDto("OPEN_SUPPORT", "secondary", "support")
            },
            "FINANCE_HOLD" => new[]
            {
                new AdminDriverWorkflowActionDto("OPEN_FINANCE", "warning", "finance"),
                new AdminDriverWorkflowActionDto("CLEAR_FINANCE_HOLD", "success", "finance"),
                new AdminDriverWorkflowActionDto("OPEN_SUPPORT", "secondary", "support")
            },
            "OFFER_RESTRICTED" => new[]
            {
                new AdminDriverWorkflowActionDto("CLEAR_DRIVER_RESTRICTIONS", "success", "overview"),
                new AdminDriverWorkflowActionDto("OPEN_OPERATIONS", "primary", "operations"),
                new AdminDriverWorkflowActionDto("OPEN_SUPPORT", "secondary", "support")
            },
            "LOCATION_RESTRICTED" => new[]
            {
                new AdminDriverWorkflowActionDto("OPEN_OPERATIONS", "primary", "operations"),
                new AdminDriverWorkflowActionDto("OPEN_SUPPORT", "secondary", "support")
            },
            "LOGIN_LOCKED" => new[]
            {
                new AdminDriverWorkflowActionDto("OPEN_SUPPORT", "secondary", "support"),
                new AdminDriverWorkflowActionDto("OPEN_OPERATIONS", "primary", "operations")
            },
            "ACTIVE_DELIVERY" => new[]
            {
                new AdminDriverWorkflowActionDto("OPEN_OPERATIONS", "primary", "operations"),
                new AdminDriverWorkflowActionDto("OPEN_SUPPORT", "secondary", "support"),
                new AdminDriverWorkflowActionDto("OPEN_FINANCE", "secondary", "finance")
            },
            "READY_FOR_DISPATCH" => new[]
            {
                new AdminDriverWorkflowActionDto("OPEN_OPERATIONS", "primary", "operations"),
                new AdminDriverWorkflowActionDto("OPEN_SUPPORT", "secondary", "support"),
                new AdminDriverWorkflowActionDto("SUSPEND_DRIVER", "danger", "overview"),
                new AdminDriverWorkflowActionDto("BAN_DRIVER", "danger", "overview")
            },
            _ => new[]
            {
                new AdminDriverWorkflowActionDto("MARK_READY_FOR_DISPATCH", "success", "operations"),
                new AdminDriverWorkflowActionDto("OPEN_OPERATIONS", "primary", "operations"),
                new AdminDriverWorkflowActionDto("OPEN_SUPPORT", "secondary", "support")
            }
        };

        var lifecycleStages = BuildAdminLifecycleStages(state);
        return new AdminDriverWorkflowSectionDto(state, readiness, blockers, alerts, actions, lifecycleStages);
    }

    private static AdminDriverLifecycleStageDto[] BuildAdminLifecycleStages(string state)
    {
        var verificationState = state switch
        {
            "PENDING_DOCUMENTS" => "attention",
            "VERIFICATION_REVIEW" => "current",
            _ => "completed"
        };

        var readinessState = state switch
        {
            "READY_TO_ACTIVATE" => "current",
            "FINANCE_HOLD" => "attention",
            "READY_FOR_DISPATCH" or "ACTIVE_DELIVERY" => "completed",
            "SUSPENDED" or "BANNED" => "attention",
            "COMPLIANCE_REVIEW" => "attention",
            _ => "upcoming"
        };

        var dispatchState = state switch
        {
            "READY_FOR_DISPATCH" => "current",
            "ACTIVE_DELIVERY" => "completed",
            _ => "upcoming"
        };

        var missionState = state switch
        {
            "ACTIVE_DELIVERY" => "current",
            "READY_FOR_DISPATCH" => "upcoming",
            _ => "upcoming"
        };

        var financeState = state switch
        {
            "FINANCE_HOLD" => "current",
            "ACTIVE_DELIVERY" or "READY_FOR_DISPATCH" => "completed",
            _ => "upcoming"
        };

        return
        [
            new AdminDriverLifecycleStageDto("verification", verificationState),
            new AdminDriverLifecycleStageDto("readiness", readinessState),
            new AdminDriverLifecycleStageDto("dispatch", dispatchState),
            new AdminDriverLifecycleStageDto("mission", missionState),
            new AdminDriverLifecycleStageDto("finance", financeState)
        ];
    }

    private static string ResolveAdminWorkflowState(
        Driver driver,
        int activeTasks,
        decimal pendingBalance,
        AdminDriverIncidentDto[] incidents,
        IReadOnlyCollection<string> missingRequirements,
        DriverCommitmentSummaryDto commitmentSummary,
        bool isLoginLocked)
    {
        if (driver.Status == AccountStatus.Banned)
        {
            return "BANNED";
        }

        if (driver.Status == AccountStatus.Suspended)
        {
            return "SUSPENDED";
        }

        if (isLoginLocked)
        {
            return "LOGIN_LOCKED";
        }

        if (driver.HasExpiredRequiredDocuments())
        {
            return "PENDING_DOCUMENTS";
        }

        // Driver needs to upload documents or was rejected — driver action required
        if (driver.VerificationStatus is DriverVerificationStatus.NeedsDocuments or DriverVerificationStatus.Rejected)
        {
            return "PENDING_DOCUMENTS";
        }

        // Documents submitted, waiting for admin review — admin action required
        if (driver.VerificationStatus == DriverVerificationStatus.UnderReview)
        {
            return "VERIFICATION_REVIEW";
        }

        if (incidents.Any(i => !string.Equals(i.Status, DriverIncidentStatus.Resolved.ToString(), StringComparison.OrdinalIgnoreCase)))
        {
            return "COMPLIANCE_REVIEW";
        }

        if (pendingBalance > 0)
        {
            return "FINANCE_HOLD";
        }

        if (driver.IsLocationUpdatesBlocked)
        {
            return "LOCATION_RESTRICTED";
        }

        if (!commitmentSummary.CanReceiveOffers)
        {
            return "OFFER_RESTRICTED";
        }

        if (activeTasks > 0)
        {
            return "ACTIVE_DELIVERY";
        }

        if (driver.IsAvailable)
        {
            return "READY_FOR_DISPATCH";
        }

        return "READY_TO_ACTIVATE";
    }

    private static int? ResolveDurationMinutes(DateTime? acceptedAtUtc, DateTime? deliveredAtUtc, DateTime? failedAtUtc)
    {
        if (!acceptedAtUtc.HasValue)
        {
            return null;
        }

        var end = deliveredAtUtc ?? failedAtUtc;
        if (!end.HasValue || end <= acceptedAtUtc)
        {
            return null;
        }

        return Math.Max(1, (int)Math.Round((end.Value - acceptedAtUtc.Value).TotalMinutes));
    }

    private static AdminDriverSupportSectionDto BuildAdminSupportSection(
        AdminDriverNoteDto[] notes,
        AdminDriverIncidentDto[] incidents,
        OrderSupportCase[] accountSupportCases,
        IReadOnlyCollection<string> missingRequirements,
        decimal pendingBalance)
    {
        var followUps = new List<AdminDriverSupportFollowUpDto>();

        if (missingRequirements.Contains("missing_documents"))
        {
            followUps.Add(new AdminDriverSupportFollowUpDto("complete_missing_documents", "today", "warning"));
        }

        if (incidents.Any(i => !string.Equals(i.Status, DriverIncidentStatus.Resolved.ToString(), StringComparison.OrdinalIgnoreCase)))
        {
            followUps.Add(new AdminDriverSupportFollowUpDto("review_open_incident", "today", "danger"));
        }

        if (pendingBalance > 0)
        {
            followUps.Add(new AdminDriverSupportFollowUpDto("clear_finance_hold", "this_week", "warning"));
        }

        var tickets = accountSupportCases
            .Select(supportCase => new AdminDriverSupportTicketDto(
                supportCase.Id,
                "DRIVER_ACCOUNT_APPEAL",
                MapDriverAccountSupportStatus(supportCase.Status),
                supportCase.Priority.ToString().ToUpperInvariant(),
                supportCase.AssignedAdminId.HasValue ? L("مراجع مسند", "Assigned admin") : L("عمليات المندوبين", "Driver operations"),
                supportCase.UpdatedAtUtc,
                null))
            .ToArray();

        var chatMessages = accountSupportCases
            .SelectMany(supportCase => supportCase.Activities)
            .Where(activity =>
                !activity.IsInternalOnly &&
                !string.IsNullOrWhiteSpace(activity.Note) &&
                activity.IsVisibleToRole("driver"))
            .OrderByDescending(activity => activity.CreatedAtUtc)
            .Take(12)
            .OrderBy(activity => activity.CreatedAtUtc)
            .Select(activity => new AdminDriverSupportChatMessageDto(
                string.Equals(activity.ActorRole, "driver", StringComparison.OrdinalIgnoreCase) ? "driver" : "support",
                activity.Note!,
                activity.CreatedAtUtc))
            .ToArray();

        var lastUpdateAtUtc = accountSupportCases
            .Select(supportCase => (DateTime?)supportCase.UpdatedAtUtc)
            .Concat(notes.Select(note => (DateTime?)note.CreatedAtUtc))
            .Concat(incidents.Select(incident => (DateTime?)incident.CreatedAtUtc))
            .Where(value => value.HasValue)
            .DefaultIfEmpty(null)
            .Max();

        var unresolvedSupportCases = accountSupportCases.Count(supportCase => supportCase.IsActive);
        var openIncidents = incidents.Count(i => !string.Equals(i.Status, DriverIncidentStatus.Resolved.ToString(), StringComparison.OrdinalIgnoreCase));

        return new AdminDriverSupportSectionDto(
            notes.Length,
            tickets.Length,
            followUps.Count,
            incidents.Count(i => string.Equals(i.Severity, DriverIncidentSeverity.Critical.ToString(), StringComparison.OrdinalIgnoreCase)),
            openIncidents + unresolvedSupportCases,
            lastUpdateAtUtc,
            notes.FirstOrDefault()?.AuthorName ?? incidents.FirstOrDefault()?.ReviewerName ?? "Driver operations",
            "driver_ops",
            unresolvedSupportCases > 0,
            tickets,
            chatMessages,
            followUps.ToArray());
    }

    private static string MapDriverAccountSupportStatus(OrderSupportCaseStatus status) =>
        status switch
        {
            OrderSupportCaseStatus.InReview or OrderSupportCaseStatus.Approved => "IN_PROGRESS",
            OrderSupportCaseStatus.Resolved or OrderSupportCaseStatus.Rejected => "RESOLVED",
            _ => "WAITING"
        };

    private static AdminDriverDocumentDto[] BuildAdminDriverDocuments(
        Driver driver,
        DriverDocumentApprovalOverlay? approvalOverlay = null,
        EffectiveVehicleProfileFields? effectiveProfile = null)
    {
        effectiveProfile ??= ResolveEffectiveVehicleProfileFields(driver, overlay: null);
        var nationalIdReview = driver.DocumentReviews.FirstOrDefault(item => item.Type == DriverDocumentType.NationalId);
        var driverLicenseReview = driver.DocumentReviews.FirstOrDefault(item => item.Type == DriverDocumentType.DriverLicense);
        var vehicleLicenseReview = driver.DocumentReviews.FirstOrDefault(item => item.Type == DriverDocumentType.VehicleLicense);
        var pendingPayload = approvalOverlay?.Payload;

        return
        [
            BuildAdminDriverDocumentDto(
                driver,
                DriverDocumentType.NationalId,
                "NationalId",
                ResolvePendingDocumentImageUrl(
                    approvalOverlay,
                    DriverDocumentType.NationalId,
                    pendingPayload?.NationalIdFrontImageUrl,
                    driver.NationalIdFrontImageUrl),
                ResolvePendingDocumentImageUrl(
                    approvalOverlay,
                    DriverDocumentType.NationalId,
                    pendingPayload?.NationalIdBackImageUrl,
                    driver.NationalIdBackImageUrl),
                effectiveProfile.NationalId,
                effectiveProfile.NationalIdExpiryDate,
                nationalIdReview,
                approvalOverlay),
            BuildAdminDriverDocumentDto(
                driver,
                DriverDocumentType.DriverLicense,
                "DriverLicense",
                ResolvePendingDocumentImageUrl(
                    approvalOverlay,
                    DriverDocumentType.DriverLicense,
                    pendingPayload?.LicenseImageUrl,
                    driver.LicenseImageUrl),
                null,
                effectiveProfile.LicenseNumber,
                effectiveProfile.DriverLicenseExpiryDate,
                driverLicenseReview,
                approvalOverlay),
            BuildAdminDriverDocumentDto(
                driver,
                DriverDocumentType.VehicleLicense,
                "VehicleLicense",
                ResolvePendingDocumentImageUrl(
                    approvalOverlay,
                    DriverDocumentType.VehicleLicense,
                    pendingPayload?.VehicleImageUrl,
                    driver.VehicleImageUrl),
                null,
                effectiveProfile.VehicleLicenseNumber,
                effectiveProfile.VehicleLicenseExpiryDate,
                vehicleLicenseReview,
                approvalOverlay)
        ];
    }

    private static AdminDriverDocumentDto BuildAdminDriverDocumentDto(
        Driver driver,
        DriverDocumentType documentType,
        string documentTypeLabel,
        string? imageUrl,
        string? secondaryImageUrl,
        string? number,
        DateTime? expiryDateUtc,
        DriverDocumentReview? review,
        DriverDocumentApprovalOverlay? approvalOverlay)
    {
        var hasPacket = HasAdminDocumentPacket(driver, documentType, imageUrl, secondaryImageUrl);
        var status = ResolveDriverDocumentStatus(hasPacket, expiryDateUtc, review);
        var rejectionReason = review?.RejectionReason;
        var reviewedAtUtc = review?.ReviewedAtUtc;
        var reviewedByName = review?.ReviewedByName;

        if (approvalOverlay?.DocumentTypes.Contains(documentType) == true)
        {
            if (approvalOverlay.Status == AccessApprovalStatus.Pending)
            {
                status = "review";
                rejectionReason = null;
                reviewedAtUtc = null;
                reviewedByName = null;
            }
            else if (approvalOverlay.Status == AccessApprovalStatus.Rejected)
            {
                status = "rejected";
                rejectionReason = string.IsNullOrWhiteSpace(approvalOverlay.RejectionReason)
                    ? "Document change rejected by admin."
                    : approvalOverlay.RejectionReason;
                reviewedAtUtc = approvalOverlay.DecidedAtUtc;
                reviewedByName = null;
            }
        }

        return new AdminDriverDocumentDto(
            documentTypeLabel,
            imageUrl,
            secondaryImageUrl,
            number,
            expiryDateUtc,
            status,
            BuildExpiryInfo(expiryDateUtc),
            review?.Decision.ToString(),
            rejectionReason,
            reviewedAtUtc,
            reviewedByName);
    }

    private static string? ResolvePendingDocumentImageUrl(
        DriverDocumentApprovalOverlay? approvalOverlay,
        DriverDocumentType documentType,
        string? requestedValue,
        string? currentValue)
    {
        if (approvalOverlay?.Status != AccessApprovalStatus.Pending ||
            !approvalOverlay.DocumentTypes.Contains(documentType) ||
            string.IsNullOrWhiteSpace(requestedValue))
        {
            return currentValue;
        }

        return requestedValue.Trim();
    }

    private static DriverProfileDocumentDto[] BuildDriverProfileDocuments(
        Driver driver,
        DriverDocumentApprovalOverlay? approvalOverlay = null) =>
    [
        BuildDriverProfileDocumentDto(driver, DriverDocumentType.NationalId, approvalOverlay),
        BuildDriverProfileDocumentDto(driver, DriverDocumentType.DriverLicense, approvalOverlay),
        BuildDriverProfileDocumentDto(driver, DriverDocumentType.VehicleLicense, approvalOverlay)
    ];

    private static DriverProfileDocumentDto BuildDriverProfileDocumentDto(
        Driver driver,
        DriverDocumentType type,
        DriverDocumentApprovalOverlay? approvalOverlay = null)
    {
        var review = driver.DocumentReviews.FirstOrDefault(item => item.Type == type);
        var status = type switch
        {
            DriverDocumentType.NationalId => ResolveDriverDocumentStatus(DriverProfileReadinessFactory.HasNationalIdPacket(driver), driver.NationalIdExpiryDate, review),
            DriverDocumentType.DriverLicense => ResolveDriverDocumentStatus(DriverProfileReadinessFactory.HasDriverLicensePacket(driver), driver.DriverLicenseExpiryDate, review),
            DriverDocumentType.VehicleLicense => ResolveDriverDocumentStatus(DriverProfileReadinessFactory.HasVehicleLicensePacket(driver), driver.VehicleLicenseExpiryDate, review),
            _ => "review"
        };
        var rejectionReason = review?.RejectionReason;
        var reviewedAtUtc = review?.ReviewedAtUtc;
        var reviewedByName = review?.ReviewedByName;

        if (approvalOverlay?.DocumentTypes.Contains(type) == true)
        {
            if (approvalOverlay.Status == AccessApprovalStatus.Pending)
            {
                status = "review";
                rejectionReason = null;
                reviewedAtUtc = null;
                reviewedByName = null;
            }
            else if (approvalOverlay.Status == AccessApprovalStatus.Rejected)
            {
                status = "rejected";
                rejectionReason = string.IsNullOrWhiteSpace(approvalOverlay.RejectionReason)
                    ? "Document change rejected by admin."
                    : approvalOverlay.RejectionReason;
                reviewedAtUtc = approvalOverlay.DecidedAtUtc;
                reviewedByName = null;
            }
        }

        return new DriverProfileDocumentDto(
            type.ToString(),
            status,
            rejectionReason,
            reviewedAtUtc,
            reviewedByName);
    }

    private static AdminDriverDocumentHealthDto BuildDocumentHealth(AdminDriverDocumentDto[] documents) =>
        new(
            documents.Count(d => string.Equals(d.Status, "valid", StringComparison.OrdinalIgnoreCase)),
            documents.Count(d => string.Equals(d.Status, "expiring", StringComparison.OrdinalIgnoreCase)),
            documents.Count(d => !string.Equals(d.Status, "valid", StringComparison.OrdinalIgnoreCase) &&
                                 !string.Equals(d.Status, "expiring", StringComparison.OrdinalIgnoreCase)));

    private async Task<DriverDocumentApprovalOverlay?> ResolveDriverDocumentApprovalOverlayAsync(
        Driver driver,
        CancellationToken cancellationToken)
    {
        var approval = await _context.AccessApprovalRequests
            .AsNoTracking()
            .Where(request =>
                request.TargetUserId == driver.UserId &&
                request.Action == ProfileChangeApprovalActions.DriverProfileDocuments)
            .OrderByDescending(request => request.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (approval is null || approval.Status == AccessApprovalStatus.Approved)
        {
            return null;
        }

        DriverDocumentsProfileChangePayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<DriverDocumentsProfileChangePayload>(
                approval.PayloadJson,
                JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }

        if (payload is null)
        {
            return null;
        }

        var documentTypes = ResolveChangedDocumentTypes(driver, payload);
        return documentTypes.Count == 0
            ? null
            : new DriverDocumentApprovalOverlay(
                approval.Status,
                approval.DecisionNote,
                approval.DecidedAtUtc,
                documentTypes,
                payload);
    }

    private async Task<DriverVehicleApprovalOverlay?> ResolveDriverVehicleApprovalOverlayAsync(
        Driver driver,
        CancellationToken cancellationToken)
    {
        var approval = await _context.AccessApprovalRequests
            .AsNoTracking()
            .Where(request =>
                request.TargetUserId == driver.UserId &&
                request.Action == ProfileChangeApprovalActions.DriverProfileVehicle &&
                request.Status != AccessApprovalStatus.Rejected)
            .OrderByDescending(request => request.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (approval is null)
        {
            return null;
        }

        DriverVehicleProfileChangePayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<DriverVehicleProfileChangePayload>(
                approval.PayloadJson,
                JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }

        if (payload is null)
        {
            return null;
        }

        return new DriverVehicleApprovalOverlay(approval.Status, payload);
    }

    private async Task<IReadOnlyList<DriverProfileSectionDto>> BuildDriverProfileSectionsAsync(
        Driver driver,
        DriverDocumentApprovalOverlay? documentApprovalOverlay,
        CancellationToken cancellationToken)
    {
        var personalApproval = await ResolveLatestProfileApprovalAsync(
            driver.UserId,
            ProfileChangeApprovalActions.DriverProfilePersonal,
            cancellationToken);
        var vehicleApproval = await ResolveLatestProfileApprovalAsync(
            driver.UserId,
            ProfileChangeApprovalActions.DriverProfileVehicle,
            cancellationToken);
        var documentsApproval = documentApprovalOverlay is null
            ? await ResolveLatestProfileApprovalAsync(
                driver.UserId,
                ProfileChangeApprovalActions.DriverProfileDocuments,
                cancellationToken)
            : null;

        return
        [
            BuildDriverProfileSection("personal", personalApproval),
            BuildDriverProfileSection("vehicle", vehicleApproval),
            BuildDriverProfileSection("documents", documentApprovalOverlay, documentsApproval)
        ];
    }

    private async Task<AccessApprovalRequest?> ResolveLatestProfileApprovalAsync(
        Guid targetUserId,
        string action,
        CancellationToken cancellationToken) =>
        await _context.AccessApprovalRequests
            .AsNoTracking()
            .Where(request => request.TargetUserId == targetUserId && request.Action == action)
            .OrderByDescending(request => request.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    private static DriverProfileSectionDto BuildDriverProfileSection(
        string section,
        AccessApprovalRequest? approval) =>
        new(
            section,
            ResolveProfileSectionStatus(approval?.Status),
            approval?.Status == AccessApprovalStatus.Rejected ? approval.DecisionNote : null,
            approval?.Status is AccessApprovalStatus.Approved or AccessApprovalStatus.Rejected
                ? approval.DecidedAtUtc
                : null);

    private static DriverProfileSectionDto BuildDriverProfileSection(
        string section,
        DriverDocumentApprovalOverlay? documentApprovalOverlay,
        AccessApprovalRequest? documentsApproval)
    {
        if (documentApprovalOverlay is not null)
        {
            return new DriverProfileSectionDto(
                section,
                ResolveProfileSectionStatus(documentApprovalOverlay.Status),
                documentApprovalOverlay.Status == AccessApprovalStatus.Rejected
                    ? documentApprovalOverlay.RejectionReason
                    : null,
                documentApprovalOverlay.Status is AccessApprovalStatus.Approved or AccessApprovalStatus.Rejected
                    ? documentApprovalOverlay.DecidedAtUtc
                    : null);
        }

        return BuildDriverProfileSection(section, documentsApproval);
    }

    private static string ResolveProfileSectionStatus(AccessApprovalStatus? status) =>
        status switch
        {
            AccessApprovalStatus.Pending => "review",
            AccessApprovalStatus.Rejected => "rejected",
            _ => "valid"
        };

    private static EffectiveVehicleProfileFields ResolveEffectiveVehicleProfileFields(
        Driver driver,
        DriverVehicleApprovalOverlay? overlay)
    {
        var pending = overlay?.Status == AccessApprovalStatus.Pending ? overlay.Payload : null;
        var approvedFallback = overlay?.Status == AccessApprovalStatus.Approved ? overlay.Payload : null;

        return new EffectiveVehicleProfileFields(
            ResolveProfileVehicleType(driver.VehicleType, pending?.VehicleType, approvedFallback?.VehicleType),
            CoalesceProfileValue(pending?.NationalId, driver.NationalId, approvedFallback?.NationalId),
            CoalesceProfileValue(pending?.LicenseNumber, driver.LicenseNumber, approvedFallback?.LicenseNumber),
            CoalesceProfileDate(pending?.NationalIdExpiryDate, driver.NationalIdExpiryDate, approvedFallback?.NationalIdExpiryDate),
            CoalesceProfileDate(pending?.DriverLicenseExpiryDate, driver.DriverLicenseExpiryDate, approvedFallback?.DriverLicenseExpiryDate),
            CoalesceProfileValue(pending?.VehicleLicenseNumber, driver.VehicleLicenseNumber, approvedFallback?.VehicleLicenseNumber),
            CoalesceProfileDate(pending?.VehicleLicenseExpiryDate, driver.VehicleLicenseExpiryDate, approvedFallback?.VehicleLicenseExpiryDate),
            CoalesceProfileValue(pending?.Region, driver.Region, approvedFallback?.Region),
            CoalesceProfileValue(pending?.City, driver.City, approvedFallback?.City));
    }

    private static DriverVehicleType? ResolveProfileVehicleType(
        DriverVehicleType? currentValue,
        string? pendingValue,
        string? approvedFallbackValue) =>
        DriverVehicleTypeMapper.TryParse(pendingValue, out var parsedValue)
            ? parsedValue
            : currentValue ?? DriverVehicleTypeMapper.ParseOrNull(approvedFallbackValue);

    private static string? CoalesceProfileValue(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static DateTime? CoalesceProfileDate(params DateTime?[] values) =>
        values.FirstOrDefault(value => value.HasValue);

    private static IReadOnlySet<DriverDocumentType> ResolveChangedDocumentTypes(
        Driver driver,
        DriverDocumentsProfileChangePayload payload)
    {
        var result = new HashSet<DriverDocumentType>();

        if (HasChanged(payload.NationalIdFrontImageUrl, driver.NationalIdFrontImageUrl) ||
            HasChanged(payload.NationalIdBackImageUrl, driver.NationalIdBackImageUrl))
        {
            result.Add(DriverDocumentType.NationalId);
        }

        if (HasChanged(payload.LicenseImageUrl, driver.LicenseImageUrl))
        {
            result.Add(DriverDocumentType.DriverLicense);
        }

        if (HasChanged(payload.VehicleImageUrl, driver.VehicleImageUrl))
        {
            result.Add(DriverDocumentType.VehicleLicense);
        }

        return result;
    }

    private static bool HasChanged(string? requestedValue, string? currentValue) =>
        !string.IsNullOrWhiteSpace(requestedValue) &&
        !string.Equals(requestedValue.Trim(), currentValue?.Trim(), StringComparison.Ordinal);

    private static bool HasAdminDocumentPacket(
        Driver driver,
        DriverDocumentType documentType,
        string? imageUrl,
        string? secondaryImageUrl)
    {
        if (documentType switch
            {
                DriverDocumentType.NationalId => DriverProfileReadinessFactory.HasNationalIdPacket(driver),
                DriverDocumentType.DriverLicense => DriverProfileReadinessFactory.HasDriverLicensePacket(driver),
                DriverDocumentType.VehicleLicense => DriverProfileReadinessFactory.HasVehicleLicensePacket(driver),
                _ => false
            })
        {
            return true;
        }

        return documentType switch
        {
            DriverDocumentType.NationalId =>
                !string.IsNullOrWhiteSpace(imageUrl) &&
                !string.IsNullOrWhiteSpace(secondaryImageUrl) &&
                driver.NationalIdExpiryDate.HasValue,
            DriverDocumentType.DriverLicense =>
                !string.IsNullOrWhiteSpace(imageUrl) &&
                driver.DriverLicenseExpiryDate.HasValue,
            DriverDocumentType.VehicleLicense =>
                !string.IsNullOrWhiteSpace(imageUrl) &&
                driver.VehicleLicenseExpiryDate.HasValue,
            _ => false
        };
    }

    private static string ResolveRiskLevel(
        Driver driver,
        AdminDriverIncidentDto[] incidents,
        decimal pendingBalance,
        IReadOnlyCollection<string> missingRequirements)
    {
        if (driver.Status is AccountStatus.Suspended or AccountStatus.Banned ||
            incidents.Any(i => string.Equals(i.Severity, DriverIncidentSeverity.Critical.ToString(), StringComparison.OrdinalIgnoreCase)))
        {
            return "high";
        }

        if (pendingBalance > 0 || missingRequirements.Count > 0 || incidents.Length > 0)
        {
            return "medium";
        }

        return "low";
    }

    private static AdminDriverFinanceEntryDto MapFinanceEntry(WalletTransaction transaction)
    {
        var reference = !string.IsNullOrWhiteSpace(transaction.Description)
            ? transaction.Description!
            : transaction.OrderId.HasValue
                ? $"order_{transaction.OrderId.Value.ToString("N")[..8]}"
                : transaction.ReferenceId.HasValue
                    ? $"ref_{transaction.ReferenceId.Value.ToString("N")[..8]}"
                    : $"txn_{transaction.Id.ToString("N")[..8]}";

        var method = transaction.SettlementId.HasValue
            ? "settlement"
            : transaction.PaymentId.HasValue
                ? "payment"
                : "wallet";

        var status = transaction.TxnType == WalletTxnType.Hold
            ? "pending"
            : transaction.SettlementId.HasValue ||
              transaction.TxnType is WalletTxnType.Settlement or WalletTxnType.Payout
                ? "settled"
                : "posted";

        return new AdminDriverFinanceEntryDto(
            transaction.Id,
            reference,
            transaction.TxnType.ToString(),
            status,
            transaction.Direction == "OUT" ? -transaction.Amount : transaction.Amount,
            // WalletTransaction has no fee field; callers must treat 0 as "not tracked".
            0,
            method,
            transaction.CreatedAtUtc);
    }

    private static string FormatDriverDisplayId(Guid driverId) =>
        $"DRV-{driverId.ToString("N")[..8].ToUpperInvariant()}";

    private static string ResolveCollectionPaymentStatus(decimal codOwedBalance, decimal codBlockThreshold)
    {
        if (codOwedBalance >= codBlockThreshold && codBlockThreshold > 0)
        {
            return "critical";
        }

        if (codOwedBalance > 0)
        {
            return "warning";
        }

        return "good";
    }

    private static AdminDriverPerformanceSectionDto BuildAdminPerformanceSection(
        decimal completionRate,
        decimal acceptanceRate,
        decimal commitmentScore,
        int completedTasks,
        int rejectedOffers,
        int timedOutOffers,
        IReadOnlyCollection<AssignmentStatsRow> regionAssignmentRows,
        IReadOnlyCollection<AssignmentStatsRow> fleetAssignmentRows,
        IReadOnlyCollection<Guid> regionDriverIds,
        IReadOnlyDictionary<Guid, DriverCommitmentSummaryDto> commitmentSummaries,
        AdminDriverIncidentDto[] incidents,
        decimal pendingBalance)
    {
        var zoneAcceptanceAverage = regionAssignmentRows.Any()
            ? Convert.ToDecimal(Math.Round(regionAssignmentRows.Average(row => row.Total > 0 ? (decimal)row.Completed / row.Total * 100 : 0m), 1))
            : acceptanceRate;
        var fleetAcceptanceAverage = fleetAssignmentRows.Any()
            ? Convert.ToDecimal(Math.Round(fleetAssignmentRows.Average(row => row.Total > 0 ? (decimal)row.Completed / row.Total * 100 : 0m), 1))
            : acceptanceRate;
        var zoneCompletionAverage = regionAssignmentRows.Any()
            ? Convert.ToDecimal(Math.Round(regionAssignmentRows.Average(row => row.Closed > 0 ? (decimal)row.Completed / row.Closed * 100 : 0m), 1))
            : completionRate;
        var fleetCompletionAverage = fleetAssignmentRows.Any()
            ? Convert.ToDecimal(Math.Round(fleetAssignmentRows.Average(row => row.Closed > 0 ? (decimal)row.Completed / row.Closed * 100 : 0m), 1))
            : completionRate;
        var zoneCommitmentValues = regionDriverIds.Any()
            ? commitmentSummaries
                .Where(pair => regionDriverIds.Contains(pair.Key))
                .Select(pair => pair.Value.CommitmentScore)
                .DefaultIfEmpty(commitmentScore)
            : [commitmentScore];
        var zoneCommitmentAverage = Math.Round(zoneCommitmentValues.Average(), 1);

        var fleetCommitmentValues = commitmentSummaries.Count > 0
            ? commitmentSummaries
                .Select(pair => pair.Value.CommitmentScore)
                .DefaultIfEmpty(commitmentScore)
            : [commitmentScore];
        var fleetCommitmentAverage = Math.Round(fleetCommitmentValues.Average(), 1);

        var metrics = new[]
        {
            new AdminDriverPerformanceMetricDto("acceptance_rate", acceptanceRate, $"{acceptanceRate:0}%", null, acceptanceRate >= 80 ? "success" : acceptanceRate >= 60 ? "warning" : "danger"),
            new AdminDriverPerformanceMetricDto("completion_rate", completionRate, $"{completionRate:0}%", null, completionRate >= 85 ? "success" : completionRate >= 65 ? "warning" : "danger"),
            new AdminDriverPerformanceMetricDto("completed_tasks", completedTasks, completedTasks.ToString(), null, completedTasks > 0 ? "primary" : "neutral"),
            new AdminDriverPerformanceMetricDto("commitment_score", commitmentScore, $"{commitmentScore:0}%", null, commitmentScore >= 80 ? "success" : commitmentScore >= 60 ? "warning" : "danger")
        };

        var benchmarks = new[]
        {
            new AdminDriverPerformanceBenchmarkDto("acceptance_rate", acceptanceRate, zoneAcceptanceAverage, fleetAcceptanceAverage, "%", acceptanceRate >= zoneAcceptanceAverage ? "above_zone_average" : "below_zone_average"),
            new AdminDriverPerformanceBenchmarkDto("completion_rate", completionRate, zoneCompletionAverage, fleetCompletionAverage, "%", completionRate >= zoneCompletionAverage ? "completion_stable" : "completion_needs_attention"),
            new AdminDriverPerformanceBenchmarkDto("commitment_score", commitmentScore, zoneCommitmentAverage, fleetCommitmentAverage, "%", commitmentScore >= zoneCommitmentAverage ? "commitment_above_region" : "commitment_below_region")
        };

        var strengths = new List<string>();
        if (acceptanceRate >= 80) strengths.Add("strong_acceptance_rate");
        if (completionRate >= 85) strengths.Add("strong_completion_rate");
        if (commitmentScore >= 80) strengths.Add("strong_commitment_score");
        if (strengths.Count == 0) strengths.Add("stable_baseline");

        var watchouts = new List<string>();
        if (rejectedOffers > 0) watchouts.Add("has_offer_rejections");
        if (timedOutOffers > 0) watchouts.Add("has_offer_timeouts");
        if (pendingBalance > 0) watchouts.Add("finance_hold_affects_readiness");
        if (incidents.Any()) watchouts.Add("open_compliance_signals");
        if (watchouts.Count == 0) watchouts.Add("no_critical_watchouts");

        var recommendations = new List<string>();
        if (acceptanceRate < 75) recommendations.Add("improve_offer_acceptance");
        if (completionRate < 80) recommendations.Add("reduce_failed_assignments");
        if (commitmentScore < 80) recommendations.Add("improve_commitment_discipline");
        if (recommendations.Count == 0) recommendations.Add("maintain_current_operating_band");

        var insightGroups = new[]
        {
            new AdminDriverPerformanceInsightGroupDto("strengths", "success", "verified", strengths.ToArray()),
            new AdminDriverPerformanceInsightGroupDto("watchouts", "warning", "warning", watchouts.ToArray()),
            new AdminDriverPerformanceInsightGroupDto("recommendations", "primary", "lightbulb", recommendations.ToArray())
        };

        return new AdminDriverPerformanceSectionDto(
            completionRate,
            acceptanceRate,
            commitmentScore,
            completedTasks,
            rejectedOffers,
            timedOutOffers,
            metrics,
            benchmarks,
            insightGroups);
    }

    private static AdminDriverVerificationSectionDto BuildAdminVerificationSection(
        Driver driver,
        AdminDriverVerificationChecklistItemDto[] checklist,
        IReadOnlyCollection<string> missingRequirements,
        decimal completionRate,
        decimal acceptanceRate,
        string? reviewerName)
    {
        var progress = DriverProfileReadinessFactory.GetCompletionPercent(missingRequirements.Count);
        var trustScore = Math.Clamp(
            Math.Round((completionRate * 0.4m) + (acceptanceRate * 0.2m) + (progress * 0.4m), 0),
            0m,
            100m);
        var recommendation = driver.VerificationStatus switch
        {
            DriverVerificationStatus.Approved => "accept",
            DriverVerificationStatus.NeedsDocuments => "complete",
            DriverVerificationStatus.Rejected => "complete",
            _ => "conditional"
        };

        return new AdminDriverVerificationSectionDto(
            $"APP-{driver.Id.ToString("N")[..8].ToUpperInvariant()}",
            driver.CreatedAtUtc,
            string.IsNullOrWhiteSpace(reviewerName) ? null : reviewerName.Trim(),
            trustScore,
            progress,
            recommendation,
            driver.ReviewNote ?? driver.SuspensionReason,
            checklist,
            driver.ReviewNote ?? string.Empty,
            driver.SuspensionReason ?? string.Empty,
            ["missing_documents", "quality_issue", "zone_missing"]);
    }

    private static string ResolveDriverDocumentStatus(bool hasPacket, DateTime? expiryDate, DriverDocumentReview? review)
    {
        if (!hasPacket)
        {
            return "review";
        }

        if (review?.Decision == DriverDocumentReviewDecision.Rejected)
        {
            return "rejected";
        }

        if (expiryDate.HasValue && expiryDate.Value.Date < SaudiTime.Today)
        {
            return "expiring";
        }

        if (review?.Decision == DriverDocumentReviewDecision.Approved)
        {
            return "valid";
        }

        return "review";
    }

    private static string? BuildExpiryInfo(DateTime? expiryDate) =>
        expiryDate.HasValue ? expiryDate.Value.ToString("yyyy-MM-dd") : null;

    private static string L(string ar, string en) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar" ? ar : en;

}
