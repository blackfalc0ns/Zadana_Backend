using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Social.Commands;

namespace Zadana.Application.Modules.Social.Queries;

public record GetNotificationDevicesQuery(Guid UserId) : IRequest<IReadOnlyList<NotificationDeviceDto>>;

public record GetNotificationDevicePreferencesQuery(
    Guid UserId,
    string? DeviceId,
    string? DeviceToken) : IRequest<NotificationDeviceDto>;

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
                x.DispatchPushEnabled,
                x.AssignmentPushEnabled,
                x.SupportPushEnabled,
                x.WalletPushEnabled,
                x.AccountPushEnabled,
                x.AdminDriversPushEnabled,
                x.AdminVendorsPushEnabled,
                x.AdminCatalogPushEnabled,
                x.AdminDisputesPushEnabled,
                x.AdminRefundsPushEnabled,
                x.AdminSettlementsPushEnabled,
                x.AdminSupportPushEnabled,
                x.AdminSystemPushEnabled,
                x.NotificationSound,
                x.IsActive,
                x.LastRegisteredAtUtc,
                x.LastSeenAtUtc))
            .ToListAsync(cancellationToken);
    }
}

public class GetNotificationDevicePreferencesQueryHandler : IRequestHandler<GetNotificationDevicePreferencesQuery, NotificationDeviceDto>
{
    private readonly IApplicationDbContext _context;

    public GetNotificationDevicePreferencesQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<NotificationDeviceDto> Handle(GetNotificationDevicePreferencesQuery request, CancellationToken cancellationToken)
    {
        var device = await NotificationDeviceCommandHelpers.FindOwnedDeviceAsync(
            _context,
            request.UserId,
            request.DeviceId,
            request.DeviceToken,
            cancellationToken);

        return RegisterNotificationDeviceCommandHandler.Map(device);
    }
}
