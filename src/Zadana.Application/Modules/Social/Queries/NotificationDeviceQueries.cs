using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Social.Commands;

namespace Zadana.Application.Modules.Social.Queries;

public record GetNotificationDevicesQuery(Guid UserId) : IRequest<IReadOnlyList<NotificationDeviceDto>>;

public class GetNotificationDevicesQueryHandler : IRequestHandler<GetNotificationDevicesQuery, IReadOnlyList<NotificationDeviceDto>>
{
    private readonly IApplicationDbContext _context;

    public GetNotificationDevicesQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<IReadOnlyList<NotificationDeviceDto>> Handle(GetNotificationDevicesQuery request, CancellationToken cancellationToken)
    {
        return await _context.UserPushDevices
            .AsNoTracking()
            .Where(x => x.UserId == request.UserId)
            .OrderByDescending(x => x.LastRegisteredAtUtc)
            .Select(x => new NotificationDeviceDto(
                x.Id,
                x.DeviceToken,
                x.Platform.ToString().ToLowerInvariant(),
                x.DeviceId,
                x.DeviceName,
                x.AppVersion,
                x.Locale,
                x.NotificationsEnabled,
                x.IsActive,
                x.LastRegisteredAtUtc,
                x.LastSeenAtUtc))
            .ToListAsync(cancellationToken);
    }
}
