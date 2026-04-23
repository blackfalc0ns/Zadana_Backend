using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Wallets.Enums;

namespace Zadana.Infrastructure.Modules.Delivery.Services;

public class DriverReadService : IDriverReadService
{
    private readonly IApplicationDbContext _context;

    public DriverReadService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AdminDriversListDto> GetAdminDriversAsync(
        string? search, string? city, string? status, string? verificationStatus,
        string? vehicleType, string? performance, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Drivers
            .Include(d => d.User)
            .Include(d => d.PrimaryZone)
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
            query = query.Where(d => d.PrimaryZone != null && d.PrimaryZone.City == city);

        if (!string.IsNullOrWhiteSpace(verificationStatus) && Enum.TryParse<DriverVerificationStatus>(verificationStatus, true, out var verEnum))
            query = query.Where(d => d.VerificationStatus == verEnum);

        if (!string.IsNullOrWhiteSpace(vehicleType))
            query = query.Where(d => d.VehicleType == vehicleType);

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

        var items = drivers.Select(d =>
        {
            activeTaskCounts.TryGetValue(d.Id, out var activeTasks);
            completedTaskCounts.TryGetValue(d.Id, out var completedTasks);
            latestGps.TryGetValue(d.Id, out var lastSeen);
            walletBalances.TryGetValue(d.Id, out var walletBalance);

            var totalAssignments = activeTasks + completedTasks;
            var acceptanceRate = totalAssignments > 0 ? (decimal)completedTasks / totalAssignments * 100 : 0;

            return new AdminDriverListItemDto(
                Id: d.Id,
                DriverDisplayId: $"DRV-#{44000 + Math.Abs(d.Id.GetHashCode() % 10000)}",
                FirstName: d.User.FullName.Split(' ').FirstOrDefault() ?? d.User.FullName,
                LastName: string.Join(' ', d.User.FullName.Split(' ').Skip(1)),
                PhoneNumber: d.User.PhoneNumber ?? "",
                ImageUrl: d.PersonalPhotoUrl,
                City: d.PrimaryZone?.City ?? "",
                Status: MapDriverStatus(d, activeTasks),
                VerificationStatus: d.VerificationStatus.ToString(),
                ActiveTasks: activeTasks,
                CompletedTasks: completedTasks,
                AcceptanceRate: Math.Round(acceptanceRate, 0),
                WalletBalance: walletBalance,
                Performance: DerivePerformance(acceptanceRate),
                VehicleType: d.VehicleType,
                LastSeenAt: lastSeen != default ? lastSeen : d.UpdatedAtUtc,
                Issues: DeriveIssues(d, walletBalance),
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
            .Include(d => d.PrimaryZone)
            .Include(d => d.Notes)
            .Include(d => d.Incidents)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == driverId, cancellationToken);

        if (driver is null) return null;

        // Active/completed tasks
        var activeTasks = await _context.DeliveryAssignments
            .CountAsync(a => a.DriverId == driverId &&
                a.Status != AssignmentStatus.Delivered &&
                a.Status != AssignmentStatus.Failed &&
                a.Status != AssignmentStatus.Cancelled, cancellationToken);

        var completedTasks = await _context.DeliveryAssignments
            .CountAsync(a => a.DriverId == driverId && a.Status == AssignmentStatus.Delivered, cancellationToken);

        var totalAssignments = activeTasks + completedTasks;
        var acceptanceRate = totalAssignments > 0 ? (decimal)completedTasks / totalAssignments * 100 : 0;

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
        var recentAssignments = await _context.DeliveryAssignments
            .Include(a => a.Order)
            .Where(a => a.DriverId == driverId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .Take(20)
            .Select(a => new AdminDriverAssignmentDto(
                a.Id, a.OrderId, a.Order.OrderNumber, a.Status.ToString(),
                a.AcceptedAtUtc, a.DeliveredAtUtc, a.FailedAtUtc, a.FailureReason, a.CodAmount))
            .ToArrayAsync(cancellationToken);

        // Documents
        var documents = new[]
        {
            new AdminDriverDocumentDto("NationalId", driver.NationalIdImageUrl, driver.NationalIdImageUrl != null ? "valid" : "missing", null),
            new AdminDriverDocumentDto("License", driver.LicenseImageUrl, driver.LicenseImageUrl != null ? "valid" : "missing", null),
            new AdminDriverDocumentDto("Vehicle", driver.VehicleImageUrl, driver.VehicleImageUrl != null ? "valid" : "missing", null),
            new AdminDriverDocumentDto("PersonalPhoto", driver.PersonalPhotoUrl, driver.PersonalPhotoUrl != null ? "valid" : "missing", null)
        };

        return new AdminDriverDetailDto(
            Id: driver.Id,
            DriverDisplayId: $"DRV-#{44000 + Math.Abs(driver.Id.GetHashCode() % 10000)}",
            FirstName: driver.User.FullName.Split(' ').FirstOrDefault() ?? driver.User.FullName,
            LastName: string.Join(' ', driver.User.FullName.Split(' ').Skip(1)),
            PhoneNumber: driver.User.PhoneNumber ?? "",
            Email: driver.User.Email ?? "",
            ImageUrl: driver.PersonalPhotoUrl,
            City: driver.PrimaryZone?.City ?? "",
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
            Issues: DeriveIssues(driver, walletBalance),
            CollectionPaymentStatus: walletBalance < 0 ? "critical" : walletBalance < 200 ? "warning" : "good",
            Alerts: null,
            ZoneName: driver.PrimaryZone != null ? $"{driver.PrimaryZone.City} - {driver.PrimaryZone.Name}" : null,
            PrimaryZoneId: driver.PrimaryZoneId,
            ReviewedAtUtc: driver.ReviewedAtUtc,
            ReviewNote: driver.ReviewNote,
            SuspensionReason: driver.SuspensionReason,
            Documents: documents,
            Notes: notes,
            Incidents: incidents,
            Finance: new AdminDriverFinanceSummaryDto(
                walletBalance, wallet?.PendingBalance ?? 0,
                totalEarnings, codCollected, totalSettlements, totalPayouts),
            RecentAssignments: recentAssignments);
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

    private static string[] DeriveIssues(Driver driver, decimal walletBalance)
    {
        var issues = new List<string>();
        if (driver.VerificationStatus is DriverVerificationStatus.NeedsDocuments or DriverVerificationStatus.UnderReview)
            issues.Add("warning");
        if (walletBalance < 0)
            issues.Add("payment");
        if (driver.Status == AccountStatus.Suspended)
            issues.Add("legal");
        return issues.Count > 0 ? issues.ToArray() : ["clear"];
    }
}
