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

        decimal? pickupLat = order.VendorBranch?.Latitude;
        decimal? pickupLng = order.VendorBranch?.Longitude;
        var activeZones = await _context.DeliveryZones
            .Where(zone => zone.IsActive)
            .ToListAsync(cancellationToken);

        var dispatchContext = DeliveryDispatchScoring.BuildContext(
            activeZones,
            pickupLat,
            pickupLng,
            order.Vendor?.City);

        var eligibleDrivers = await _context.Drivers
            .Include(d => d.User)
            .Include(d => d.PrimaryZone)
            .Where(d =>
                d.VerificationStatus == DriverVerificationStatus.Approved &&
                d.Status == AccountStatus.Active &&
                d.IsAvailable)
            .ToListAsync(cancellationToken);

        if (order.Status == OrderStatus.ReadyForPickup)
        {
            var note = eligibleDrivers.Count == 0
                ? "Dispatch pending: no-eligible-driver"
                : "Auto-dispatch started";

            order.ChangeStatus(OrderStatus.DriverAssignmentInProgress, null, note);
            _context.OrderStatusHistories.Add(order.StatusHistory.Last());
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        if (eligibleDrivers.Count == 0)
        {
            if (order.Status == OrderStatus.DriverAssignmentInProgress)
            {
                order.ChangeStatus(OrderStatus.DriverAssignmentInProgress, null, "Dispatch pending: no-eligible-driver");
                _context.OrderStatusHistories.Add(order.StatusHistory.Last());
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation("Auto-dispatch: No eligible drivers for order {OrderId}", orderId);
            return null;
        }

        var driverIds = eligibleDrivers.Select(d => d.Id).ToList();
        var now = DateTime.UtcNow;

        var latestLocations = await _context.DriverLocations
            .Where(l => driverIds.Contains(l.DriverId))
            .GroupBy(l => l.DriverId)
            .Select(g => g.OrderByDescending(l => l.RecordedAtUtc).First())
            .ToDictionaryAsync(l => l.DriverId, cancellationToken);

        var activeTaskCounts = await _context.DeliveryAssignments
            .Where(a => a.DriverId != null && driverIds.Contains(a.DriverId.Value) &&
                a.Status != Zadana.Domain.Modules.Delivery.Enums.AssignmentStatus.Delivered &&
                a.Status != Zadana.Domain.Modules.Delivery.Enums.AssignmentStatus.Failed &&
                a.Status != Zadana.Domain.Modules.Delivery.Enums.AssignmentStatus.Cancelled)
            .GroupBy(a => a.DriverId)
            .Select(g => new { DriverId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.DriverId!.Value, g => g.Count, cancellationToken);

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

        var candidates = eligibleDrivers.Select(driver =>
        {
            latestLocations.TryGetValue(driver.Id, out var location);
            activeTaskCounts.TryGetValue(driver.Id, out var activeTasks);
            reliabilityData.TryGetValue(driver.Id, out var reliability);

            var totalTasks = (reliability?.Completed ?? 0) + (reliability?.Failed ?? 0);
            var reliabilityScore = totalTasks > 0
                ? (decimal)(reliability!.Completed) / totalTasks * 100
                : 50m;

            var evaluation = DeliveryDispatchScoring.EvaluateCandidate(
                driver,
                location,
                activeTasks,
                reliabilityScore,
                dispatchContext,
                now);

            return new
            {
                Driver = driver,
                Evaluation = evaluation
            };
        })
        .OrderBy(c => c.Evaluation.CompositeScore)
        .ToList();

        var best = candidates.FirstOrDefault();
        if (best is null)
        {
            _logger.LogInformation("Auto-dispatch: No suitable driver for order {OrderId}", orderId);
            return null;
        }

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
        order.ChangeStatus(
            OrderStatus.DriverAssigned,
            null,
            $"Auto-dispatched using {best.Evaluation.MatchReason} to {best.Driver.User.FullName}");
        _context.OrderStatusHistories.Add(order.StatusHistory.Last());

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Auto-dispatch: Assigned driver {DriverId} to order {OrderId} ({Reason})",
            best.Driver.Id, orderId, best.Evaluation.MatchReason);

        return new DispatchDecisionDto(
            best.Driver.Id,
            best.Driver.User.FullName,
            Math.Round(best.Evaluation.DistanceKm, 1),
            best.Evaluation.ActiveTaskCount,
            Math.Round(best.Evaluation.ReliabilityScore, 1),
            best.Evaluation.GpsFresh,
            best.Evaluation.MatchReason);
    }
}
