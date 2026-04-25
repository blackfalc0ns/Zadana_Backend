using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Wallets.Enums;

namespace Zadana.Infrastructure.Modules.Delivery.Services;

public class DriverReadService : IDriverReadService
{
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

        var commitmentSummary = await _driverCommitmentPolicyService.GetDriverSummaryAsync(driverId, cancellationToken);

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
            Issues: DeriveIssues(driver, walletBalance, commitmentSummary),
            CollectionPaymentStatus: walletBalance < 0 ? "critical" : walletBalance < 200 ? "warning" : "good",
            Alerts: null,
            CommitmentScore: commitmentSummary.CommitmentScore,
            DailyRejections: commitmentSummary.DailyRejections,
            WeeklyRejections: commitmentSummary.WeeklyRejections,
            EnforcementLevel: commitmentSummary.EnforcementLevel,
            LastOfferResponseAtUtc: commitmentSummary.LastOfferResponseAtUtc,
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
            customerAddress?.PhoneNumber,
            assignment.Order.PaymentMethod.ToString(),
            assignment.CodAmount,
            assignment.RequiresPickupOtpVerification,
            ResolveOtpStatus(assignment.RequiresPickupOtpVerification, assignment.IsPickupOtpVerified),
            assignment.RequiresDeliveryOtpVerification,
            ResolveOtpStatus(assignment.RequiresDeliveryOtpVerification, assignment.IsDeliveryOtpVerified),
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
                    assignment.Order.TotalAmount,
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
            customerAddress?.PhoneNumber,
            assignment.Order.VendorBranch?.AddressLine ?? assignment.Order.Vendor.NationalAddress ?? string.Empty,
            customerAddress?.AddressLine ?? string.Empty,
            MapCompletedOrderStatus(assignment.Order.Status),
            assignment.Order.PaymentMethod.ToString(),
            assignment.Order.TotalAmount,
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
            .Include(d => d.PrimaryZone)
            .FirstOrDefaultAsync(d => d.UserId == userId, cancellationToken);

        if (driver is null)
        {
            return null;
        }

        var missingRequirements = GetMissingRequirements(driver, driver.User);
        var completionPercent = GetCompletionPercent(missingRequirements.Count);

        return new DriverProfileDto(
            driver.User.FullName,
            driver.User.Email ?? string.Empty,
            driver.User.PhoneNumber ?? string.Empty,
            driver.Address,
            driver.VehicleType?.ToString(),
            driver.LicenseNumber,
            driver.NationalId,
            driver.PersonalPhotoUrl,
            driver.NationalIdImageUrl,
            driver.LicenseImageUrl,
            driver.VehicleImageUrl,
            driver.PrimaryZoneId,
            driver.PrimaryZone is not null ? $"{driver.PrimaryZone.City} - {driver.PrimaryZone.Name}" : null,
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
                : ["mark_delivered"];
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

    private static decimal ResolveDistanceKm(Order order, CustomerAddress? customerAddress)
    {
        if (order.QuotedDistanceKm.HasValue)
        {
            return Math.Round(order.QuotedDistanceKm.Value, 2);
        }

        if (order.VendorBranch?.Latitude is null ||
            order.VendorBranch.Longitude is null ||
            customerAddress?.Latitude is null ||
            customerAddress.Longitude is null)
        {
            return 0m;
        }

        return Math.Round(ApproximateDistanceKm(
            order.VendorBranch.Latitude.Value,
            order.VendorBranch.Longitude.Value,
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

    private static List<string> GetMissingRequirements(Driver driver, Domain.Modules.Identity.Entities.User user)
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(user.FullName) ||
            string.IsNullOrWhiteSpace(user.Email) ||
            string.IsNullOrWhiteSpace(user.PhoneNumber) ||
            string.IsNullOrWhiteSpace(driver.Address))
        {
            missing.Add("missing_personal_info");
        }

        if (driver.VehicleType is null ||
            string.IsNullOrWhiteSpace(driver.LicenseNumber) ||
            string.IsNullOrWhiteSpace(driver.NationalId))
        {
            missing.Add("missing_vehicle_info");
        }

        if (string.IsNullOrWhiteSpace(driver.PersonalPhotoUrl) ||
            string.IsNullOrWhiteSpace(driver.NationalIdImageUrl) ||
            string.IsNullOrWhiteSpace(driver.LicenseImageUrl) ||
            string.IsNullOrWhiteSpace(driver.VehicleImageUrl))
        {
            missing.Add("missing_documents");
        }

        if (!driver.PrimaryZoneId.HasValue)
        {
            missing.Add("missing_zone_selection");
        }

        return missing;
    }

    private static int GetCompletionPercent(int missingCount) =>
        missingCount switch
        {
            <= 0 => 100,
            1 => 75,
            2 => 50,
            3 => 25,
            _ => 0
        };
}
