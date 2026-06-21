using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Social.Enums;

namespace Zadana.Application.Modules.Social.Commands;

public sealed record CleanupStaleDriverOfferNotificationsCommand(Guid UserId) : IRequest<int>;

public sealed class CleanupStaleDriverOfferNotificationsCommandHandler
    : IRequestHandler<CleanupStaleDriverOfferNotificationsCommand, int>
{
    private readonly IApplicationDbContext _context;

    public CleanupStaleDriverOfferNotificationsCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int> Handle(
        CleanupStaleDriverOfferNotificationsCommand request,
        CancellationToken cancellationToken)
    {
        var driverId = await _context.Drivers
            .AsNoTracking()
            .Where(driver => driver.UserId == request.UserId)
            .Select(driver => (Guid?)driver.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var offerNotifications = _context.Notifications
            .Where(notification =>
                notification.UserId == request.UserId &&
                notification.Type == NotificationTypes.DriverDeliveryOffer &&
                notification.Data != null &&
                notification.Data.Contains("dispatch.offer_new"));

        if (!driverId.HasValue)
        {
            var orphaned = await offerNotifications.ToListAsync(cancellationToken);
            _context.Notifications.RemoveRange(orphaned);
            return orphaned.Count == 0
                ? 0
                : await _context.SaveChangesAsync(cancellationToken);
        }

        var now = DateTime.UtcNow;
        var activeOfferOrderIds = await _context.DeliveryAssignments
            .AsNoTracking()
            .Where(assignment =>
                assignment.DriverId == driverId.Value &&
                assignment.Status == AssignmentStatus.OfferSent &&
                assignment.OfferExpiresAtUtc.HasValue &&
                assignment.OfferExpiresAtUtc > now)
            .Select(assignment => assignment.OrderId)
            .ToListAsync(cancellationToken);

        var stale = await offerNotifications
            .Where(notification =>
                !notification.ReferenceId.HasValue ||
                !activeOfferOrderIds.Contains(notification.ReferenceId.Value))
            .ToListAsync(cancellationToken);

        if (stale.Count == 0)
        {
            return 0;
        }

        _context.Notifications.RemoveRange(stale);
        await _context.SaveChangesAsync(cancellationToken);
        return stale.Count;
    }
}
