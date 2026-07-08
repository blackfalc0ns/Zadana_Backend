using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Delivery.Commands.UpdateDriverLocation;

public class UpdateDriverLocationCommandHandler : IRequestHandler<UpdateDriverLocationCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOrderTrackingRealtimeNotifier _orderTrackingRealtimeNotifier;
    private readonly ILogger<UpdateDriverLocationCommandHandler> _logger;

    public UpdateDriverLocationCommandHandler(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IOrderTrackingRealtimeNotifier orderTrackingRealtimeNotifier,
        ILogger<UpdateDriverLocationCommandHandler> logger)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _orderTrackingRealtimeNotifier = orderTrackingRealtimeNotifier;
        _logger = logger;
    }

    public async Task<Unit> Handle(UpdateDriverLocationCommand request, CancellationToken cancellationToken)
    {
        var driver = await _context.Drivers.FindAsync([request.DriverId], cancellationToken)
            ?? throw new NotFoundException("Driver", request.DriverId);

        if (driver.IsLocationUpdatesBlocked)
        {
            throw new BusinessRuleException(
                "DRIVER_LOCATION_UPDATES_BLOCKED",
                "أوقفنا تحديثات الموقع لهذا المندوب من الإدارة | Location updates are currently blocked for this driver.");
        }

        var location = new DriverLocation(driver.Id, request.Latitude, request.Longitude, request.AccuracyMeters);
        _context.DriverLocations.Add(location);

        // Maintain the single-row latest projection alongside the audit table.
        // Customers and the admin order detail page query this row directly,
        // turning a top-1 indexed scan into a primary-key seek.
        var latest = await _context.DriverLatestLocations
            .FirstOrDefaultAsync(x => x.DriverId == driver.Id, cancellationToken);
        if (latest is null)
        {
            _context.DriverLatestLocations.Add(new DriverLatestLocation(
                driver.Id,
                request.Latitude,
                request.Longitude,
                request.AccuracyMeters,
                location.RecordedAtUtc));
        }
        else
        {
            latest.Update(
                request.Latitude,
                request.Longitude,
                request.AccuracyMeters,
                location.RecordedAtUtc);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogDebug(
            "[DriverTracking] Persisted location for driver {DriverId} at ({Lat},{Lng}) acc={Acc}m. Looking for active assignments...",
            driver.Id, request.Latitude, request.Longitude, request.AccuracyMeters);

        await BroadcastToActiveOrdersAsync(driver.Id, location, cancellationToken);

        return Unit.Value;
    }

    private async Task BroadcastToActiveOrdersAsync(
        Guid driverId,
        DriverLocation location,
        CancellationToken cancellationToken)
    {
        // The driver may be carrying more than one assignment in the same trip,
        // so push the location to every active order they are servicing.
        var activeOrderIds = await _context.DeliveryAssignments
            .AsNoTracking()
            .Where(a =>
                a.DriverId == driverId &&
                a.Status != AssignmentStatus.SearchingDriver &&
                a.Status != AssignmentStatus.OfferSent &&
                a.Status != AssignmentStatus.Rejected &&
                a.Status != AssignmentStatus.Cancelled &&
                a.Status != AssignmentStatus.Delivered &&
                a.Status != AssignmentStatus.Failed)
            .Select(a => a.OrderId)
            .ToListAsync(cancellationToken);

        if (activeOrderIds.Count == 0)
        {
            _logger.LogDebug(
                "[DriverTracking] Driver {DriverId} has NO active assignments. Location was saved but no broadcast was sent.",
                driverId);
            return;
        }

        _logger.LogDebug(
            "[DriverTracking] Driver {DriverId} has {Count} active order(s). Broadcasting to: {OrderIds}",
            driverId, activeOrderIds.Count, string.Join(", ", activeOrderIds));

        foreach (var orderId in activeOrderIds)
        {
            try
            {
                await _orderTrackingRealtimeNotifier.BroadcastDriverLocationAsync(
                    orderId,
                    driverId,
                    location.Latitude,
                    location.Longitude,
                    location.AccuracyMeters,
                    location.RecordedAtUtc,
                    cancellationToken);

                _logger.LogDebug(
                    "[DriverTracking] Broadcast sent for order {OrderId} (group: order-{OrderIdN}).",
                    orderId, orderId.ToString("N"));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "[DriverTracking] Failed to broadcast driver {DriverId} location to order {OrderId} subscribers.",
                    driverId,
                    orderId);
            }
        }
    }
}
