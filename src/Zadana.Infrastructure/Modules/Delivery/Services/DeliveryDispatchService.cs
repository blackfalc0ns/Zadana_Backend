using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Orders.Events;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Infrastructure.Modules.Delivery.Services;

public class DeliveryDispatchService : IDeliveryDispatchService
{
    private static readonly TimeSpan OfferTtl = TimeSpan.FromSeconds(25);
    private static readonly TimeSpan PickupOtpTtl = TimeSpan.FromHours(12);
    private const int MaxAutoOfferAttempts = 3;

    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeliveryDispatchService> _logger;
    private readonly IPublisher _publisher;
    private readonly INotificationService _notificationService;
    private readonly IDriverCommitmentPolicyService _driverCommitmentPolicyService;

    public DeliveryDispatchService(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        ILogger<DeliveryDispatchService> logger,
        IPublisher publisher,
        INotificationService notificationService,
        IDriverCommitmentPolicyService driverCommitmentPolicyService)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _publisher = publisher;
        _notificationService = notificationService;
        _driverCommitmentPolicyService = driverCommitmentPolicyService;
    }

    public async Task<DispatchDecisionDto?> TryAutoDispatchAsync(
        Guid orderId,
        bool resetCycle = false,
        CancellationToken cancellationToken = default)
    {
        await ProcessExpiredOffersAsync(cancellationToken);

        var order = await _context.Orders
            .Include(item => item.Vendor)
            .Include(item => item.VendorBranch)
            .FirstOrDefaultAsync(item => item.Id == orderId, cancellationToken);

        if (order is null)
        {
            _logger.LogWarning("Dispatch offer engine: order {OrderId} not found.", orderId);
            return null;
        }

        if (order.Status is not (OrderStatus.ReadyForPickup or OrderStatus.DriverAssignmentInProgress))
        {
            _logger.LogInformation("Dispatch offer engine: order {OrderId} is in {Status}.", orderId, order.Status);
            return null;
        }

        var assignment = await _context.DeliveryAssignments
            .FirstOrDefaultAsync(item => item.OrderId == orderId, cancellationToken);

        if (resetCycle)
        {
            await ResetDispatchCycleAsync(orderId, assignment, cancellationToken);
            assignment = await _context.DeliveryAssignments
                .FirstOrDefaultAsync(item => item.OrderId == orderId, cancellationToken);
        }

        var now = DateTime.UtcNow;

        if (assignment is not null &&
            assignment.Status == AssignmentStatus.OfferSent &&
            assignment.DriverId.HasValue &&
            assignment.OfferExpiresAtUtc.HasValue &&
            assignment.OfferExpiresAtUtc.Value > now)
        {
            return await BuildExistingOfferDecisionAsync(assignment, cancellationToken);
        }

        if (order.Status == OrderStatus.ReadyForPickup)
        {
            order.ChangeStatus(OrderStatus.DriverAssignmentInProgress, null, "Auto-dispatch started");
            _context.OrderStatusHistories.Add(order.StatusHistory.Last());
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return await OfferNextDriverAsync(order, assignment, cancellationToken);
    }

    public async Task ProcessExpiredOffersAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var expiredAssignments = await _context.DeliveryAssignments
            .Include(item => item.Order)
                .ThenInclude(order => order.Vendor)
            .Include(item => item.Order)
                .ThenInclude(order => order.VendorBranch)
            .Where(item =>
                item.Status == AssignmentStatus.OfferSent &&
                item.OfferExpiresAtUtc.HasValue &&
                item.OfferExpiresAtUtc.Value <= now)
            .ToListAsync(cancellationToken);

        foreach (var assignment in expiredAssignments)
        {
            var order = assignment.Order;
            if (order.Status is not (OrderStatus.ReadyForPickup or OrderStatus.DriverAssignmentInProgress))
            {
                continue;
            }

            var timedOutAttempt = await _context.DeliveryOfferAttempts
                .Where(item => item.OrderId == order.Id && item.Status == DeliveryOfferAttemptStatus.Offered)
                .OrderByDescending(item => item.AttemptNumber)
                .FirstOrDefaultAsync(cancellationToken);

            assignment.MarkOfferTimedOut();
            if (timedOutAttempt is not null)
            {
                timedOutAttempt.MarkTimedOut();
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (assignment.DriverId.HasValue)
            {
                await _driverCommitmentPolicyService.ApplyOperationalEnforcementAsync([assignment.DriverId.Value], cancellationToken);
            }

            await OfferNextDriverAsync(order, assignment, cancellationToken);
        }
    }

    public async Task<DriverOfferActionResultDto> AcceptOfferAsync(
        Guid assignmentId,
        Guid driverId,
        CancellationToken cancellationToken = default)
    {
        await ProcessExpiredOffersAsync(cancellationToken);

        var assignment = await _context.DeliveryAssignments
            .Include(item => item.Order)
                .ThenInclude(order => order.Vendor)
            .Include(item => item.Driver)
                .ThenInclude(driver => driver!.User)
            .FirstOrDefaultAsync(item => item.Id == assignmentId, cancellationToken)
            ?? throw new NotFoundException("DeliveryAssignment", assignmentId);

        if (assignment.DriverId != driverId || assignment.Status != AssignmentStatus.OfferSent)
        {
            throw new BusinessRuleException("DELIVERY_OFFER_NOT_AVAILABLE", "The delivery offer is no longer available.");
        }

        if (!assignment.OfferExpiresAtUtc.HasValue || assignment.OfferExpiresAtUtc.Value <= DateTime.UtcNow)
        {
            throw new BusinessRuleException("DELIVERY_OFFER_EXPIRED", "The delivery offer has expired.");
        }

        assignment.Accept();

        var attempt = await _context.DeliveryOfferAttempts
            .Where(item => item.OrderId == assignment.OrderId && item.DriverId == driverId && item.Status == DeliveryOfferAttemptStatus.Offered)
            .OrderByDescending(item => item.AttemptNumber)
            .FirstOrDefaultAsync(cancellationToken);

        attempt?.MarkAccepted();

        var pickupOtp = assignment.EnsurePickupOtp(PickupOtpTtl);

        var oldStatus = assignment.Order.Status;
        if (assignment.Order.Status != OrderStatus.DriverAssigned)
        {
            assignment.Order.ChangeStatus(OrderStatus.DriverAssigned, null, "Driver accepted delivery offer.");
            _context.OrderStatusHistories.Add(assignment.Order.StatusHistory.Last());
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _publisher.Publish(
            new OrderStatusChangedNotification(
                assignment.OrderId,
                assignment.Order.UserId,
                assignment.Order.VendorId,
                assignment.Order.OrderNumber,
                oldStatus,
                assignment.Order.Status,
                NotifyCustomer: true,
                NotifyVendor: false,
                ActorRole: "dispatch"),
            cancellationToken);

        if (assignment.Order.Vendor is not null)
        {
            var driverName = assignment.Driver?.User.FullName ?? "Assigned driver";
            var driverPhone = assignment.Driver?.User.PhoneNumber ?? string.Empty;
            var vehicleType = assignment.Driver?.VehicleType?.ToString() ?? "Unknown";
            var plateNumber = assignment.Driver?.LicenseNumber ?? "N/A";

            await _notificationService.SendToUserAsync(
                assignment.Order.Vendor.UserId,
                "تم تعيين المندوب للطلب",
                "Driver assigned to the order",
                $"تم تعيين {driverName} ({vehicleType} - {plateNumber}) لطلب {assignment.Order.OrderNumber}. رمز الاستلام هو {pickupOtp}.",
                $"{driverName} ({vehicleType} - {plateNumber}) has been assigned to order #{assignment.Order.OrderNumber}. Pickup OTP: {pickupOtp}.",
                "vendor-driver-assigned",
                assignment.OrderId,
                $"assignmentId={assignment.Id};pickupOtp={pickupOtp};driverPhone={driverPhone}",
                cancellationToken);
        }

        return new DriverOfferActionResultDto(
            assignment.Id,
            assignment.OrderId,
            assignment.Status.ToString(),
            "Offer accepted successfully.");
    }

    public async Task<DriverOfferActionResultDto> RejectOfferAsync(
        Guid assignmentId,
        Guid driverId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        await ProcessExpiredOffersAsync(cancellationToken);

        var assignment = await _context.DeliveryAssignments
            .Include(item => item.Order)
                .ThenInclude(order => order.Vendor)
            .Include(item => item.Order)
                .ThenInclude(order => order.VendorBranch)
            .FirstOrDefaultAsync(item => item.Id == assignmentId, cancellationToken)
            ?? throw new NotFoundException("DeliveryAssignment", assignmentId);

        if (assignment.DriverId != driverId || assignment.Status != AssignmentStatus.OfferSent)
        {
            throw new BusinessRuleException("DELIVERY_OFFER_NOT_AVAILABLE", "The delivery offer is no longer available.");
        }

        assignment.Reject(reason);

        var attempt = await _context.DeliveryOfferAttempts
            .Where(item => item.OrderId == assignment.OrderId && item.DriverId == driverId && item.Status == DeliveryOfferAttemptStatus.Offered)
            .OrderByDescending(item => item.AttemptNumber)
            .FirstOrDefaultAsync(cancellationToken);

        attempt?.MarkRejected(reason);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _driverCommitmentPolicyService.ApplyOperationalEnforcementAsync([driverId], cancellationToken);

        await OfferNextDriverAsync(assignment.Order, assignment, cancellationToken);

        return new DriverOfferActionResultDto(
            assignment.Id,
            assignment.OrderId,
            AssignmentStatus.Rejected.ToString(),
            "Offer rejected. Waiting for the next offer.");
    }

    private async Task<DispatchDecisionDto?> OfferNextDriverAsync(
        Domain.Modules.Orders.Entities.Order order,
        DeliveryAssignment? existingAssignment,
        CancellationToken cancellationToken)
    {
        var unsuccessfulAttempts = await _context.DeliveryOfferAttempts
            .Where(item =>
                item.OrderId == order.Id &&
                item.Status != DeliveryOfferAttemptStatus.Accepted)
            .ToListAsync(cancellationToken);

        if (unsuccessfulAttempts.Count >= MaxAutoOfferAttempts)
        {
            await TrackDispatchQueueNoteAsync(order, "Dispatch pending: offer-timeout-exhausted", cancellationToken);
            return null;
        }

        var busyDriverIds = await _context.DeliveryAssignments
            .Where(item =>
                item.DriverId.HasValue &&
                item.OrderId != order.Id &&
                item.Status != AssignmentStatus.Delivered &&
                item.Status != AssignmentStatus.Failed &&
                item.Status != AssignmentStatus.Cancelled &&
                item.Status != AssignmentStatus.Rejected &&
                item.Status != AssignmentStatus.Returned)
            .Select(item => item.DriverId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var excludedDriverIds = unsuccessfulAttempts
            .Select(item => item.DriverId)
            .Distinct()
            .ToHashSet();

        decimal? pickupLat = order.VendorBranch?.Latitude;
        decimal? pickupLng = order.VendorBranch?.Longitude;

        var activeZones = await _context.DeliveryZones
            .Where(zone => zone.IsActive)
            .ToListAsync(cancellationToken);

        var dispatchContext = DeliveryDispatchScoring.BuildContext(
            activeZones,
            pickupLat,
            pickupLng,
            order.Vendor?.City,
            order.Vendor?.Region);

        var eligibleDrivers = await _context.Drivers
            .Include(driver => driver.User)
            .Where(driver =>
                driver.VerificationStatus == DriverVerificationStatus.Approved &&
                driver.Status == AccountStatus.Active &&
                driver.IsAvailable &&
                !busyDriverIds.Contains(driver.Id))
            .ToListAsync(cancellationToken);

        eligibleDrivers = eligibleDrivers
            .Where(driver => !excludedDriverIds.Contains(driver.Id))
            .ToList();

        if (eligibleDrivers.Count == 0)
        {
            await TrackDispatchQueueNoteAsync(order, "Dispatch pending: no-eligible-driver", cancellationToken);
            return null;
        }

        var driverIds = eligibleDrivers.Select(driver => driver.Id).ToList();
        await _driverCommitmentPolicyService.ApplyOperationalEnforcementAsync(driverIds, cancellationToken);
        var commitmentSummaries = await _driverCommitmentPolicyService.GetDriverSummariesAsync(driverIds, cancellationToken);

        eligibleDrivers = eligibleDrivers
            .Where(driver =>
                commitmentSummaries.TryGetValue(driver.Id, out var summary) &&
                summary.CanReceiveOffers)
            .ToList();

        if (eligibleDrivers.Count == 0)
        {
            await TrackDispatchQueueNoteAsync(order, "Dispatch pending: soft-blocked-by-rejections", cancellationToken);
            return null;
        }

        driverIds = eligibleDrivers.Select(driver => driver.Id).ToList();
        var now = DateTime.UtcNow;

        var latestLocations = await _context.DriverLocations
            .Where(location => driverIds.Contains(location.DriverId))
            .GroupBy(location => location.DriverId)
            .Select(group => group.OrderByDescending(location => location.RecordedAtUtc).First())
            .ToDictionaryAsync(location => location.DriverId, cancellationToken);

        var activeTaskCounts = await _context.DeliveryAssignments
            .Where(item =>
                item.DriverId != null &&
                driverIds.Contains(item.DriverId.Value) &&
                item.Status != AssignmentStatus.Delivered &&
                item.Status != AssignmentStatus.Failed &&
                item.Status != AssignmentStatus.Cancelled &&
                item.Status != AssignmentStatus.Rejected &&
                item.Status != AssignmentStatus.Returned)
            .GroupBy(item => item.DriverId)
            .Select(group => new { DriverId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.DriverId!.Value, item => item.Count, cancellationToken);

        var reliabilityData = await _context.DeliveryAssignments
            .Where(item =>
                item.DriverId != null &&
                driverIds.Contains(item.DriverId.Value) &&
                (item.Status == AssignmentStatus.Delivered || item.Status == AssignmentStatus.Failed))
            .GroupBy(item => item.DriverId)
            .Select(group => new
            {
                DriverId = group.Key,
                Completed = group.Count(item => item.Status == AssignmentStatus.Delivered),
                Failed = group.Count(item => item.Status == AssignmentStatus.Failed)
            })
            .ToDictionaryAsync(item => item.DriverId!.Value, cancellationToken);

        var candidates = eligibleDrivers
            .Select(driver =>
            {
                latestLocations.TryGetValue(driver.Id, out var location);
                activeTaskCounts.TryGetValue(driver.Id, out var activeTasks);
                reliabilityData.TryGetValue(driver.Id, out var reliability);

                var totalTasks = (reliability?.Completed ?? 0) + (reliability?.Failed ?? 0);
                var reliabilityScore = totalTasks > 0
                    ? (decimal)reliability!.Completed / totalTasks * 100
                    : 50m;

                return new
                {
                    Driver = driver,
                    CommitmentSummary = commitmentSummaries.TryGetValue(driver.Id, out var summary)
                        ? summary
                        : new DriverCommitmentSummaryDto(0, 0, 0, 0, 0, 100m, "Healthy", true, null, null),
                    Evaluation = DeliveryDispatchScoring.EvaluateCandidate(
                        driver,
                        location,
                        activeTasks,
                        reliabilityScore,
                        commitmentSummaries.TryGetValue(driver.Id, out var commitment)
                            ? commitment.CommitmentScore
                            : 100m,
                        dispatchContext,
                        now)
                };
            })
            .OrderBy(item => item.Evaluation.CompositeScore)
            .ToList();

        var best = candidates.FirstOrDefault();
        if (best is null)
        {
            await TrackDispatchQueueNoteAsync(order, "Dispatch pending: no-eligible-driver", cancellationToken);
            return null;
        }

        var assignment = existingAssignment;
        if (assignment is null)
        {
            var codAmount = order.PaymentMethod == PaymentMethodType.CashOnDelivery ? order.TotalAmount : 0m;
            assignment = new DeliveryAssignment(order.Id, codAmount);
            _context.DeliveryAssignments.Add(assignment);
        }
        else if (assignment.Status == AssignmentStatus.Accepted)
        {
            return null;
        }

        var attemptNumber = unsuccessfulAttempts.Count + 1;
        var expiresAtUtc = now.Add(OfferTtl);

        assignment.OfferTo(best.Driver.Id, attemptNumber, expiresAtUtc);

        _context.DeliveryOfferAttempts.Add(
            new DeliveryOfferAttempt(
                order.Id,
                assignment.Id,
                best.Driver.Id,
                attemptNumber,
                expiresAtUtc));

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Send real-time SignalR notification so the driver's Home screen updates instantly.
        // SendToUserAsync persists to DB inbox AND pushes via SignalR simultaneously.
        await _notificationService.SendToUserAsync(
            best.Driver.UserId,
            "طلب جديد للمندوب",
            "New delivery offer",
            $"لديك طلب جديد من {order.Vendor?.BusinessNameAr ?? best.Driver.User.FullName} ويجب الرد خلال ثوانٍ قليلة.",
            $"You have a new delivery offer and need to respond within a few seconds.",
            "driver-offer",
            order.Id,
            $"assignmentId={assignment.Id}",
            cancellationToken);

        _logger.LogInformation(
            "Dispatch offer engine: offered order {OrderId} to driver {DriverId} attempt {Attempt} ({Reason}).",
            order.Id,
            best.Driver.Id,
            attemptNumber,
            best.Evaluation.MatchReason);

        return new DispatchDecisionDto(
            best.Driver.Id,
            best.Driver.User.FullName,
            Math.Round(best.Evaluation.DistanceKm, 1),
            best.Evaluation.ActiveTaskCount,
            Math.Round(best.Evaluation.ReliabilityScore, 1),
            best.Evaluation.CommitmentScore,
            best.Evaluation.GpsFresh,
            best.Evaluation.MatchReason,
            best.Evaluation.CommitmentAdjustmentReason);
    }

    private async Task ResetDispatchCycleAsync(
        Guid orderId,
        DeliveryAssignment? assignment,
        CancellationToken cancellationToken)
    {
        var staleAttempts = await _context.DeliveryOfferAttempts
            .Where(item => item.OrderId == orderId && item.Status != DeliveryOfferAttemptStatus.Accepted)
            .ToListAsync(cancellationToken);

        if (staleAttempts.Count > 0)
        {
            _context.DeliveryOfferAttempts.RemoveRange(staleAttempts);
        }

        assignment?.ResetForRedispatch();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task TrackDispatchQueueNoteAsync(
        Domain.Modules.Orders.Entities.Order order,
        string note,
        CancellationToken cancellationToken)
    {
        if (order.Status != OrderStatus.DriverAssignmentInProgress)
        {
            return;
        }

        var latestNote = order.StatusHistory
            .LastOrDefault(item => item.NewStatus == OrderStatus.DriverAssignmentInProgress)
            ?.Note;

        if (string.Equals(latestNote, note, StringComparison.Ordinal))
        {
            return;
        }

        order.ChangeStatus(OrderStatus.DriverAssignmentInProgress, null, note);
        _context.OrderStatusHistories.Add(order.StatusHistory.Last());
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<DispatchDecisionDto?> BuildExistingOfferDecisionAsync(
        DeliveryAssignment assignment,
        CancellationToken cancellationToken)
    {
        if (!assignment.DriverId.HasValue)
        {
            return null;
        }

        var driver = await _context.Drivers
            .Include(item => item.User)
            .FirstOrDefaultAsync(item => item.Id == assignment.DriverId.Value, cancellationToken);

        if (driver is null)
        {
            return null;
        }

        var latestLocation = await _context.DriverLocations
            .Where(item => item.DriverId == driver.Id)
            .OrderByDescending(item => item.RecordedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var commitmentSummary = await _driverCommitmentPolicyService.GetDriverSummaryAsync(driver.Id, cancellationToken);

        return new DispatchDecisionDto(
            driver.Id,
            driver.User.FullName,
            latestLocation is null ? 0m : 0m,
            0,
            50m,
            commitmentSummary.CommitmentScore,
            latestLocation is not null && (DateTime.UtcNow - latestLocation.RecordedAtUtc) <= DeliveryDispatchScoring.GpsFreshnessThreshold,
            "offer-already-sent",
            commitmentSummary.CanReceiveOffers ? null : "soft-blocked-by-rejections");
    }
}
