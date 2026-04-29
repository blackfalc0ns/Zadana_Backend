using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;

namespace Zadana.Infrastructure.Modules.Delivery.Services;

public class DriverReadService : IDriverReadService
{
    private sealed record AssignmentStatsRow(Guid DriverId, int Total, int Completed, int Closed);

    private readonly IApplicationDbContext _context;
    private readonly IDriverCommitmentPolicyService _driverCommitmentPolicyService;

    public DriverReadService(
        IApplicationDbContext context,
        IDriverCommitmentPolicyService driverCommitmentPolicyService)
    {
        _context = context;
        _driverCommitmentPolicyService = driverCommitmentPolicyService;
    }

    public async Task<AdminDriversListDto> GetAdminDriversAsync(
        string? search, string? city, string? status, string? verificationStatus,
        string? vehicleType, string? performance, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Drivers
            .Include(d => d.User)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(d =>
                d.User.FullName.ToLower().Contains(term) ||
                d.User.PhoneNumber!.Contains(term) ||
                d.NationalId!.Contains(term));
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

        var driverIds = drivers.Select(d => d.Id).ToList();

        // Load active task counts
        var activeTaskCounts = await _context.DeliveryAssignments
            .Where(a => driverIds.Contains(a.DriverId!.Value) &&
                a.Status != AssignmentStatus.Delivered &&
                a.Status != AssignmentStatus.Failed &&
                a.Status != AssignmentStatus.Cancelled &&
                a.Status != AssignmentStatus.Returned)
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

        // Load wallet balances
        var walletBalances = await _context.Wallets
            .Where(w => w.OwnerType == WalletOwnerType.Driver && driverIds.Contains(w.OwnerId))
            .ToDictionaryAsync(w => w.OwnerId, w => w.CurrentBalance, cancellationToken);

        var commitmentSummaries = await _driverCommitmentPolicyService.GetDriverSummariesAsync(driverIds, cancellationToken);

        var items = drivers.Select(d =>
        {
            activeTaskCounts.TryGetValue(d.Id, out var activeTasks);
            completedTaskCounts.TryGetValue(d.Id, out var completedTasks);
            latestGps.TryGetValue(d.Id, out var lastSeen);
            walletBalances.TryGetValue(d.Id, out var walletBalance);
            commitmentSummaries.TryGetValue(d.Id, out var commitmentSummary);
            commitmentSummary ??= new DriverCommitmentSummaryDto(0, 0, 0, 0, 0, 100m, "Healthy", true, null, null);

            var totalAssignments = activeTasks + completedTasks;
            var acceptanceRate = totalAssignments > 0 ? (decimal)completedTasks / totalAssignments * 100 : 0;

            return new AdminDriverListItemDto(
                Id: d.Id,
                DriverDisplayId: $"DRV-#{44000 + Math.Abs(d.Id.GetHashCode() % 10000)}",
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
                Issues: DeriveIssues(d, walletBalance, commitmentSummary),
                CollectionPaymentStatus: walletBalance < 0 ? "critical" : walletBalance < 200 ? "warning" : "good",
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
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == driverId, cancellationToken);

        if (driver is null) return null;

        var missingRequirements = DriverProfileReadinessFactory.GetMissingRequirements(driver, driver.User);

        // Active/completed tasks
        var activeTasks = await _context.DeliveryAssignments
            .CountAsync(a => a.DriverId == driverId &&
                a.Status != AssignmentStatus.Delivered &&
                a.Status != AssignmentStatus.Failed &&
                a.Status != AssignmentStatus.Cancelled, cancellationToken);

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

        // Finance summary
        var totalEarnings = wallet is not null
            ? await _context.WalletTransactions
                .Where(t => t.WalletId == wallet.Id && t.Direction == "IN")
                .SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0
            : 0;

        var codCollected = await _context.DeliveryAssignments
            .Where(a => a.DriverId == driverId && a.Status == AssignmentStatus.Delivered)
            .SumAsync(a => (decimal?)a.CodAmount, cancellationToken) ?? 0;

        var totalSettlements = await _context.Settlements
            .CountAsync(s => s.DriverId == driverId, cancellationToken);

        var totalPayouts = await _context.Payouts
            .CountAsync(p => p.Settlement.DriverId == driverId, cancellationToken);

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
        var documents = new[]
        {
            new AdminDriverDocumentDto("NationalIdFront", driver.NationalIdFrontImageUrl, driver.NationalIdFrontImageUrl != null ? "valid" : "review", null),
            new AdminDriverDocumentDto("NationalIdBack", driver.NationalIdBackImageUrl, driver.NationalIdBackImageUrl != null ? "valid" : "review", null),
            new AdminDriverDocumentDto("License", driver.LicenseImageUrl, driver.LicenseImageUrl != null ? "valid" : "review", null),
            new AdminDriverDocumentDto("Vehicle", driver.VehicleImageUrl, driver.VehicleImageUrl != null ? "valid" : "review", null),
            new AdminDriverDocumentDto("PersonalPhoto", driver.PersonalPhotoUrl, driver.PersonalPhotoUrl != null ? "valid" : "review", null)
        };

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

        var latestPendingPayoutAtUtc = await _context.Payouts
            .Where(p => p.Settlement.DriverId == driverId &&
                (p.Status == PayoutStatus.Pending || p.Status == PayoutStatus.Processing))
            .OrderBy(p => p.CreatedAtUtc)
            .Select(p => (DateTime?)p.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var primaryPayoutMethod = await _context.DriverPayoutMethods
            .AsNoTracking()
            .Where(p => p.DriverId == driverId && p.IsPrimary)
            .OrderByDescending(p => p.IsVerified)
            .ThenByDescending(p => p.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

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
            missingRequirements);

        var workflow = BuildAdminWorkflowSection(workflowState);
        var overview = new AdminDriverOverviewSectionDto(
            driver.Address,
            driver.Region,
            driver.City,
            driver.LicenseNumber,
            Math.Round(completionRate, 0),
            commitmentSummary.CommitmentScore,
            walletBalance < 0 ? "critical" : walletBalance < 200 ? "warning" : "good");
        var operations = new AdminDriverOperationsSectionDto(
            driver.Region,
            driver.City,
            lastLocation?.Latitude,
            lastLocation?.Longitude,
            lastLocation?.AccuracyMeters,
            lastLocation?.RecordedAtUtc,
            activeDriversInCity,
            averageDeliveryMinutes,
            null,
            recentAssignmentRows.Select(a => new AdminDriverOperationTaskDto(
                a.Id,
                string.IsNullOrWhiteSpace(a.VendorName) ? $"Order {a.OrderNumber}" : a.VendorName,
                driver.City ?? driver.User.FullName,
                a.Status,
                a.AcceptedAtUtc ?? a.CreatedAtUtc,
                ResolveDurationMinutes(a.AcceptedAtUtc, a.DeliveredAtUtc, a.FailedAtUtc),
                a.FailureReason,
                a.CodAmount)).ToArray());
        var support = BuildAdminSupportSection(
            notes,
            incidents,
            missingRequirements,
            wallet?.PendingBalance ?? 0);
        var documentHealth = BuildDocumentHealth(documents);
        var compliance = new AdminDriverComplianceSectionDto(
            incidents.Count(i => !string.Equals(i.Status, DriverIncidentStatus.Resolved.ToString(), StringComparison.OrdinalIgnoreCase)),
            incidents.Count(i => string.Equals(i.Severity, DriverIncidentSeverity.Critical.ToString(), StringComparison.OrdinalIgnoreCase)),
            incidents.Count(i => !string.Equals(i.Severity, DriverIncidentSeverity.Medium.ToString(), StringComparison.OrdinalIgnoreCase)),
            documents.Count(d => !string.Equals(d.Status, "valid", StringComparison.OrdinalIgnoreCase)),
            driver.Status == AccountStatus.Suspended ? 1 : 0,
            ResolveRiskLevel(driver, incidents, wallet?.PendingBalance ?? 0, missingRequirements),
            documentHealth);
        var financeDetails = new AdminDriverFinanceSectionDto(
            walletBalance,
            wallet?.PendingBalance ?? 0,
            codCollected,
            Math.Max(0, wallet?.PendingBalance ?? 0),
            latestPendingPayoutAtUtc,
            primaryPayoutMethod?.MethodType.ToString(),
            $"live_{totalSettlements}_settlements_{totalPayouts}_payouts",
            walletTransactions.Select(MapFinanceEntry).ToArray());
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
        var profileReadiness = DriverProfileReadinessFactory.BuildAdminReadiness(driver, driver.User);
        var verification = BuildAdminVerificationSection(
            driver,
            profileReadiness.Checklist,
            profileReadiness.MissingRequirements,
            Math.Round(completionRate, 0),
            Math.Round(acceptanceRate, 0));

        return new AdminDriverDetailDto(
            Id: driver.Id,
            DriverDisplayId: $"DRV-#{44000 + Math.Abs(driver.Id.GetHashCode() % 10000)}",
            FirstName: driver.User.FullName.Split(' ').FirstOrDefault() ?? driver.User.FullName,
            LastName: string.Join(' ', driver.User.FullName.Split(' ').Skip(1)),
            PhoneNumber: driver.User.PhoneNumber ?? "",
            Email: driver.User.Email ?? "",
            ImageUrl: driver.PersonalPhotoUrl,
            City: driver.City ?? "",
            Status: MapDriverStatus(driver, activeTasks),
            VerificationStatus: driver.VerificationStatus.ToString(),
            VehicleType: driver.VehicleType,
            JoinedAt: driver.CreatedAtUtc,
            LastSeenAt: lastLocation?.RecordedAtUtc ?? driver.UpdatedAtUtc,
            ActiveTasks: activeTasks,
            CompletedTasks: completedTasks,
            AcceptanceRate: Math.Round(acceptanceRate, 0),
            WalletBalance: walletBalance,
            Performance: DerivePerformance(acceptanceRate),
            Issues: DeriveIssues(driver, walletBalance, commitmentSummary),
            CollectionPaymentStatus: walletBalance < 0 ? "critical" : walletBalance < 200 ? "warning" : "good",
            Alerts: null,
            CommitmentScore: commitmentSummary.CommitmentScore,
            DailyRejections: commitmentSummary.DailyRejections,
            WeeklyRejections: commitmentSummary.WeeklyRejections,
            EnforcementLevel: commitmentSummary.EnforcementLevel,
            LastOfferResponseAtUtc: commitmentSummary.LastOfferResponseAtUtc,
            Address: driver.Address,
            LicenseNumber: driver.LicenseNumber,

            ReviewedAtUtc: driver.ReviewedAtUtc,
            ReviewNote: driver.ReviewNote,
            SuspensionReason: driver.SuspensionReason,
            ProfileReadiness: profileReadiness,
            Documents: documents,
            Notes: notes,
            Incidents: incidents,
            Finance: new AdminDriverFinanceSummaryDto(
                walletBalance, wallet?.PendingBalance ?? 0,
                totalEarnings, codCollected, totalSettlements, totalPayouts),
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
            .Include(a => a.Driver)
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.DriverId == driverId, cancellationToken);

        if (assignment is null)
        {
            return null;
        }

        var customerAddress = await _context.CustomerAddresses
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == assignment.Order.CustomerAddressId, cancellationToken);

        return new DriverAssignmentDetailDto(
            assignment.Id,
            assignment.OrderId,
            assignment.Order.OrderNumber,
            assignment.Status.ToString(),
            ResolveAssignmentHomeState(assignment),
            ResolveAllowedActions(assignment, assignment.Order.Status),
            assignment.Order.Vendor.BusinessNameEn,
            assignment.Order.VendorBranch?.AddressLine ?? assignment.Order.Vendor.NationalAddress ?? string.Empty,
            assignment.Order.VendorBranch?.Latitude,
            assignment.Order.VendorBranch?.Longitude,
            assignment.Order.Vendor.ContactPhone,
            customerAddress?.ContactName ?? "Customer",
            customerAddress?.AddressLine ?? string.Empty,
            customerAddress?.Latitude,
            customerAddress?.Longitude,
            customerAddress?.ContactPhone,
            assignment.Order.PaymentMethod.ToString(),
            ResolveCodAmount(assignment),
            assignment.RequiresPickupOtpVerification,
            ResolveOtpStatus(assignment.RequiresPickupOtpVerification, assignment.IsPickupOtpVerified),
            assignment.RequiresDeliveryOtpVerification,
            ResolveOtpStatus(assignment.RequiresDeliveryOtpVerification, assignment.IsDeliveryOtpVerified),
            assignment.IsInHandoffWindow ? assignment.PickupOtpCode : null,
            ResolveArrivalState(assignment),
            assignment.Order.Items
                .Select(item => new DriverAssignmentItemDto(item.ProductName, item.Quantity, item.UnitPrice, item.LineTotal))
                .ToArray());
    }

    public async Task<DriverCompletedOrdersListDto> GetCompletedOrdersAsync(
        Guid driverId,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedStatus = string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToLowerInvariant();
        var query = _context.DeliveryAssignments
            .AsNoTracking()
            .Include(a => a.Order)
                .ThenInclude(o => o.Vendor)
            .Include(a => a.Order)
                .ThenInclude(o => o.Items)
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

        var assignments = await query
            .OrderByDescending(a => a.DeliveredAtUtc ?? a.FailedAtUtc ?? a.Order.CancelledAtUtc ?? a.UpdatedAtUtc)
            .ToListAsync(cancellationToken);

        var addressIds = assignments.Select(a => a.Order.CustomerAddressId).Distinct().ToArray();
        var addresses = await _context.CustomerAddresses
            .AsNoTracking()
            .Where(a => addressIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        var items = assignments
            .Select(assignment =>
            {
                addresses.TryGetValue(assignment.Order.CustomerAddressId, out var customerAddress);

                return new DriverCompletedOrderListItemDto(
                    assignment.OrderId,
                    assignment.Order.Vendor.BusinessNameEn,
                    customerAddress?.ContactName ?? "Customer",
                    ResolveCompletedAtUtc(assignment),
                    MapCompletedOrderStatus(assignment.Order.Status),
                    ResolveCodAmount(assignment),
                    ResolveDistanceKm(assignment.Order, customerAddress),
                    assignment.Order.PaymentMethod.ToString(),
                    customerAddress?.AddressLine ?? string.Empty,
                    assignment.Order.Items
                        .Select(item => new DriverCompletedOrderItemDto(item.ProductName, item.Quantity, item.UnitPrice, item.LineTotal))
                        .ToArray());
            })
            .ToArray();

        return new DriverCompletedOrdersListDto(items, items.Length);
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
            assignment.Order.Vendor.BusinessNameEn,
            assignment.Order.Vendor.ContactPhone,
            customerAddress?.ContactName ?? "Customer",
            customerAddress?.ContactPhone,
            assignment.Order.VendorBranch?.AddressLine ?? assignment.Order.Vendor.NationalAddress ?? string.Empty,
            customerAddress?.AddressLine ?? string.Empty,
            MapCompletedOrderStatus(assignment.Order.Status),
            assignment.Order.PaymentMethod.ToString(),
            ResolveCodAmount(assignment),
            assignment.Order.DeliveryFee,
            ResolveDistanceKm(assignment.Order, customerAddress),
            ResolveCompletedAtUtc(assignment),
            assignment.Order.Items
                .Select(item => new DriverCompletedOrderItemDto(item.ProductName, item.Quantity, item.UnitPrice, item.LineTotal))
                .ToArray());
    }

    public async Task<DriverProfileDto?> GetDriverProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var driver = await _context.Drivers
            .AsNoTracking()
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.UserId == userId, cancellationToken);

        if (driver is null)
        {
            return null;
        }

        var missingRequirements = DriverProfileReadinessFactory.GetMissingRequirements(driver, driver.User);
        var completionPercent = DriverProfileReadinessFactory.GetCompletionPercent(missingRequirements.Count);

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

        return new DriverProfileDto(
            driver.User.FullName,
            driver.User.Email ?? string.Empty,
            driver.User.PhoneNumber ?? string.Empty,
            driver.Address,
            driver.VehicleType?.ToString(),
            driver.LicenseNumber,
            driver.NationalId,
            driver.PersonalPhotoUrl,
            driver.NationalIdFrontImageUrl,
            driver.NationalIdBackImageUrl,
            driver.LicenseImageUrl,
            driver.VehicleImageUrl,
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
            missingRequirements.Count == 0,
            completionPercent,
            missingRequirements,
            missingRequirements.Count == 0);
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
        if (d.Status == AccountStatus.Suspended) return "Suspended";
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
        if (walletBalance < 0)
            issues.Add("payment");
        if (driver.Status == AccountStatus.Suspended)
            issues.Add("legal");
        if (!commitmentSummary.CanReceiveOffers)
            issues.Add("dispatch");
        return issues.Count > 0 ? issues.ToArray() : ["clear"];
    }

    private static string ResolveAssignmentHomeState(DeliveryAssignment assignment) =>
        assignment.Status == AssignmentStatus.OfferSent ? "IncomingOffer" : "OnMission";

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
            // After arrival at vendor:
            // - If pickup OTP verified (vendor confirmed handoff) → driver can mark picked up
            // - Otherwise → driver waits for vendor to confirm pickup via OTP
            return [];
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
        if (assignment.Status == AssignmentStatus.ArrivedAtCustomer || assignment.ArrivedAtCustomerAtUtc.HasValue)
        {
            return "arrived_at_customer";
        }

        if (assignment.Status == AssignmentStatus.ArrivedAtVendor || assignment.ArrivedAtVendorAtUtc.HasValue)
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
            _ => "BLOCKED"
        };

        var blockers = state switch
        {
            "SUSPENDED" => ["suspension_active"],
            "PENDING_DOCUMENTS" => ["missing_documents"],
            "VERIFICATION_REVIEW" => ["verification_in_progress"],
            "COMPLIANCE_REVIEW" => ["open_compliance_case"],
            "FINANCE_HOLD" => ["finance_hold"],
            _ => Array.Empty<string>()
        };

        var alerts = state switch
        {
            "ACTIVE_DELIVERY" => ["driver_on_active_mission"],
            "READY_FOR_DISPATCH" => ["ready_for_dispatch"],
            "READY_TO_ACTIVATE" => ["driver_offline_but_approved"],
            _ => Array.Empty<string>()
        };

        var actions = state switch
        {
            "SUSPENDED" => new[]
            {
                new AdminDriverWorkflowActionDto("REVIEW_COMPLIANCE", "warning", "compliance"),
                new AdminDriverWorkflowActionDto("OPEN_FINANCE", "secondary", "finance"),
                new AdminDriverWorkflowActionDto("REACTIVATE_DRIVER", "success", "overview")
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
                new AdminDriverWorkflowActionDto("OPEN_SUPPORT", "secondary", "support")
            },
            "FINANCE_HOLD" => new[]
            {
                new AdminDriverWorkflowActionDto("OPEN_FINANCE", "warning", "finance"),
                new AdminDriverWorkflowActionDto("CLEAR_FINANCE_HOLD", "success", "finance"),
                new AdminDriverWorkflowActionDto("OPEN_SUPPORT", "secondary", "support")
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
                new AdminDriverWorkflowActionDto("SUSPEND_DRIVER", "danger", "overview")
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
            "SUSPENDED" => "attention",
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
        IReadOnlyCollection<string> missingRequirements)
    {
        if (driver.Status == AccountStatus.Suspended)
        {
            return "SUSPENDED";
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

        return new AdminDriverSupportSectionDto(
            notes.Length,
            0,
            followUps.Count,
            incidents.Count(i => string.Equals(i.Severity, DriverIncidentSeverity.Critical.ToString(), StringComparison.OrdinalIgnoreCase)),
            incidents.Count(i => !string.Equals(i.Status, DriverIncidentStatus.Resolved.ToString(), StringComparison.OrdinalIgnoreCase)),
            notes.FirstOrDefault()?.CreatedAtUtc,
            notes.FirstOrDefault()?.AuthorName ?? incidents.FirstOrDefault()?.ReviewerName,
            "operations",
            false,
            [],
            [],
            followUps.ToArray());
    }

    private static AdminDriverDocumentHealthDto BuildDocumentHealth(AdminDriverDocumentDto[] documents) =>
        new(
            documents.Count(d => string.Equals(d.Status, "valid", StringComparison.OrdinalIgnoreCase)),
            documents.Count(d => string.Equals(d.Status, "expiring", StringComparison.OrdinalIgnoreCase)),
            documents.Count(d => !string.Equals(d.Status, "valid", StringComparison.OrdinalIgnoreCase) &&
                                 !string.Equals(d.Status, "expiring", StringComparison.OrdinalIgnoreCase)));

    private static string ResolveRiskLevel(
        Driver driver,
        AdminDriverIncidentDto[] incidents,
        decimal pendingBalance,
        IReadOnlyCollection<string> missingRequirements)
    {
        if (driver.Status == AccountStatus.Suspended ||
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
        var reference = transaction.OrderId.HasValue
            ? $"order_{transaction.OrderId.Value.ToString("N")[..8]}"
            : transaction.ReferenceId.HasValue
                ? $"ref_{transaction.ReferenceId.Value.ToString("N")[..8]}"
                : $"txn_{transaction.Id.ToString("N")[..8]}";

        var method = transaction.SettlementId.HasValue
            ? "settlement"
            : transaction.PaymentId.HasValue
                ? "payment"
                : "wallet";

        return new AdminDriverFinanceEntryDto(
            transaction.Id,
            reference,
            transaction.TxnType.ToString(),
            "posted",
            transaction.Direction == "OUT" ? -transaction.Amount : transaction.Amount,
            0,
            method,
            transaction.CreatedAtUtc);
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
        var zoneCommitmentAverage = regionDriverIds.Any()
            ? Math.Round(commitmentSummaries
                .Where(pair => regionDriverIds.Contains(pair.Key))
                .DefaultIfEmpty(new KeyValuePair<Guid, DriverCommitmentSummaryDto>(Guid.Empty, new DriverCommitmentSummaryDto(0, 0, 0, 0, 0, commitmentScore, "Healthy", true, null, null)))
                .Average(pair => pair.Value.CommitmentScore), 1)
            : commitmentScore;
        var fleetCommitmentAverage = commitmentSummaries.Count > 0
            ? Math.Round(commitmentSummaries.Average(pair => pair.Value.CommitmentScore), 1)
            : commitmentScore;

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
        decimal acceptanceRate)
    {
        var progress = DriverProfileReadinessFactory.GetCompletionPercent(missingRequirements.Count);
        var trustScore = Math.Max(25m, Math.Min(98m, Math.Round((completionRate * 0.4m) + (acceptanceRate * 0.2m) + (progress * 0.4m), 0)));
        var recommendation = driver.VerificationStatus switch
        {
            DriverVerificationStatus.Approved => "accept",
            DriverVerificationStatus.NeedsDocuments => "complete",
            DriverVerificationStatus.Rejected => "complete",
            _ => "conditional"
        };

        return new AdminDriverVerificationSectionDto(
            $"APP-{Math.Abs(driver.Id.GetHashCode() % 100000):D5}",
            driver.CreatedAtUtc,
            driver.ReviewedByUserId.HasValue ? "operations_desk" : null,
            trustScore,
            progress,
            recommendation,
            driver.ReviewNote ?? driver.SuspensionReason,
            checklist,
            driver.ReviewNote ?? string.Empty,
            driver.SuspensionReason ?? string.Empty,
            ["missing_documents", "quality_issue", "zone_missing"]);
    }

}
