using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;

namespace Zadana.Infrastructure.Modules.Delivery.Services;

public class DeliveryDispatchService : IDeliveryDispatchService
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeliveryDispatchService> _logger;

    private static readonly TimeSpan GpsFreshnessThreshold = TimeSpan.FromMinutes(30);

    public DeliveryDispatchService(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        ILogger<DeliveryDispatchService> logger)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<DispatchDecisionDto?> TryAutoDispatchAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders
            .Include(o => o.VendorBranch)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null)
        {
            _logger.LogWarning("Auto-dispatch: Order {OrderId} not found", orderId);
            return null;
        }

        if (order.Status != OrderStatus.ReadyForPickup && order.Status != OrderStatus.DriverAssignmentInProgress)
        {
            _logger.LogInformation("Auto-dispatch: Order {OrderId} is in {Status}, skipping", orderId, order.Status);
            return null;
        }

        // Transition to DriverAssignmentInProgress if not already
        if (order.Status == OrderStatus.ReadyForPickup)
        {
            order.ChangeStatus(OrderStatus.DriverAssignmentInProgress, null, "Auto-dispatch started");
            _context.OrderStatusHistories.Add(order.StatusHistory.Last());
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // Get pickup coordinates from vendor branch
        decimal? pickupLat = order.VendorBranch?.Latitude;
        decimal? pickupLng = order.VendorBranch?.Longitude;
        var branchCity = order.VendorBranch is not null
            ? await _context.DeliveryZones
                .Where(z => z.IsActive)
                .OrderBy(z => Math.Abs(z.CenterLat - order.VendorBranch.Latitude) + Math.Abs(z.CenterLng - order.VendorBranch.Longitude))
                .Select(z => z.City)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        // Find eligible drivers: Approved + Active + Available
        var eligibleDrivers = await _context.Drivers
            .Include(d => d.User)
            .Include(d => d.PrimaryZone)
            .Where(d =>
                d.VerificationStatus == DriverVerificationStatus.Approved &&
                d.Status == AccountStatus.Active &&
                d.IsAvailable)
            .ToListAsync(cancellationToken);

        if (eligibleDrivers.Count == 0)
        {
            _logger.LogInformation("Auto-dispatch: No eligible drivers for order {OrderId}", orderId);
            return null;
        }

        var driverIds = eligibleDrivers.Select(d => d.Id).ToList();
        var now = DateTime.UtcNow;

        // Load latest GPS for each driver
        var latestLocations = await _context.DriverLocations
            .Where(l => driverIds.Contains(l.DriverId))
            .GroupBy(l => l.DriverId)
            .Select(g => g.OrderByDescending(l => l.RecordedAtUtc).First())
            .ToDictionaryAsync(l => l.DriverId, cancellationToken);

        // Load active task counts
        var activeTaskCounts = await _context.DeliveryAssignments
            .Where(a => a.DriverId != null && driverIds.Contains(a.DriverId.Value) &&
                a.Status != Zadana.Domain.Modules.Delivery.Enums.AssignmentStatus.Delivered &&
                a.Status != Zadana.Domain.Modules.Delivery.Enums.AssignmentStatus.Failed &&
                a.Status != Zadana.Domain.Modules.Delivery.Enums.AssignmentStatus.Cancelled)
            .GroupBy(a => a.DriverId)
            .Select(g => new { DriverId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.DriverId!.Value, g => g.Count, cancellationToken);

        // Load reliability data (completed vs failed)
        var reliabilityData = await _context.DeliveryAssignments
            .Where(a => a.DriverId != null && driverIds.Contains(a.DriverId.Value) &&
                (a.Status == Zadana.Domain.Modules.Delivery.Enums.AssignmentStatus.Delivered ||
                 a.Status == Zadana.Domain.Modules.Delivery.Enums.AssignmentStatus.Failed))
            .GroupBy(a => a.DriverId)
            .Select(g => new
            {
                DriverId = g.Key,
                Completed = g.Count(a => a.Status == Zadana.Domain.Modules.Delivery.Enums.AssignmentStatus.Delivered),
                Failed = g.Count(a => a.Status == Zadana.Domain.Modules.Delivery.Enums.AssignmentStatus.Failed)
            })
            .ToDictionaryAsync(g => g.DriverId!.Value, cancellationToken);

        // Score and rank drivers
        var candidates = eligibleDrivers.Select(driver =>
        {
            latestLocations.TryGetValue(driver.Id, out var location);
            activeTaskCounts.TryGetValue(driver.Id, out var activeTasks);
            reliabilityData.TryGetValue(driver.Id, out var reliability);

            var gpsFresh = location is not null && (now - location.RecordedAtUtc) < GpsFreshnessThreshold;
            var distanceKm = 999m;
            var matchReason = "no-match";

            if (gpsFresh && pickupLat.HasValue && pickupLng.HasValue && location is not null)
            {
                distanceKm = ApproximateDistanceKm(location.Latitude, location.Longitude, pickupLat.Value, pickupLng.Value);
                matchReason = "gps-proximity";
            }
            else if (driver.PrimaryZone?.City == branchCity)
            {
                distanceKm = 10m; // Approximate zone-level distance
                matchReason = gpsFresh ? "zone-match" : "zone-match-stale-gps";
            }
            else if (driver.PrimaryZone is not null)
            {
                distanceKm = 50m; // Different city fallback
                matchReason = "city-fallback";
            }

            var totalTasks = (reliability?.Completed ?? 0) + (reliability?.Failed ?? 0);
            var reliabilityScore = totalTasks > 0
                ? (decimal)(reliability!.Completed) / totalTasks * 100
                : 50m; // New driver gets neutral score

            // Composite score: lower is better
            var score = distanceKm * 2
                + activeTasks * 15
                - reliabilityScore * 0.5m
                + (gpsFresh ? 0 : 20);

            return new
            {
                Driver = driver,
                Score = score,
                DistanceKm = distanceKm,
                ActiveTasks = activeTasks,
                ReliabilityScore = reliabilityScore,
                GpsFresh = gpsFresh,
                MatchReason = matchReason
            };
        })
        .OrderBy(c => c.Score)
        .ToList();

        var best = candidates.FirstOrDefault();
        if (best is null || best.MatchReason == "no-match")
        {
            _logger.LogInformation("Auto-dispatch: No suitable driver for order {OrderId}", orderId);
            return null;
        }

        // Create or update assignment
        var assignment = await _context.DeliveryAssignments
            .FirstOrDefaultAsync(a => a.OrderId == orderId, cancellationToken);

        if (assignment is null)
        {
            var codAmount = order.PaymentMethod == PaymentMethodType.CashOnDelivery ? order.TotalAmount : 0;
            assignment = new DeliveryAssignment(orderId, codAmount);
            _context.DeliveryAssignments.Add(assignment);
        }

        assignment.OfferTo(best.Driver.Id);
        assignment.Accept();
        order.ChangeStatus(OrderStatus.DriverAssigned, null, $"Auto-dispatched to {best.Driver.User.FullName} ({best.MatchReason})");
        _context.OrderStatusHistories.Add(order.StatusHistory.Last());

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Auto-dispatch: Assigned driver {DriverId} to order {OrderId} ({Reason})",
            best.Driver.Id, orderId, best.MatchReason);

        return new DispatchDecisionDto(
            best.Driver.Id,
            best.Driver.User.FullName,
            Math.Round(best.DistanceKm, 1),
            best.ActiveTasks,
            Math.Round(best.ReliabilityScore, 1),
            best.GpsFresh,
            best.MatchReason);
    }

    private static decimal ApproximateDistanceKm(decimal lat1, decimal lng1, decimal lat2, decimal lng2)
    {
        // Simple equirectangular approximation (good enough for short distances in Saudi Arabia)
        var dLat = (double)(lat2 - lat1) * Math.PI / 180;
        var dLng = (double)(lng2 - lng1) * Math.PI / 180;
        var avgLat = (double)(lat1 + lat2) / 2 * Math.PI / 180;

        var x = dLng * Math.Cos(avgLat);
        var y = dLat;
        var distKm = Math.Sqrt(x * x + y * y) * 6371;

        return (decimal)Math.Round(distKm, 2);
    }
}
