using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Social.Commands;

public record NotificationDeviceDto(
    Guid Id,
    string DeviceToken,
    string Platform,
    string? DeviceId,
    string? DeviceName,
    string? AppVersion,
    string? Locale,
    bool NotificationsEnabled,
    bool IsActive,
    DateTime LastRegisteredAtUtc,
    DateTime LastSeenAtUtc);

public record RegisterNotificationDeviceCommand(
    Guid UserId,
    string DeviceToken,
    string Platform,
    string? DeviceId,
    string? DeviceName,
    string? AppVersion,
    string? Locale,
    bool NotificationsEnabled = true) : IRequest<NotificationDeviceDto>;

public class RegisterNotificationDeviceCommandHandler : IRequestHandler<RegisterNotificationDeviceCommand, NotificationDeviceDto>
{
    private readonly IApplicationDbContext _context;

    public RegisterNotificationDeviceCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<NotificationDeviceDto> Handle(RegisterNotificationDeviceCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceToken))
        {
            throw new BadRequestException("DEVICE_TOKEN_REQUIRED", "Device token is required.");
        }

        if (!Enum.TryParse<PushPlatform>(request.Platform, true, out var platform))
        {
            throw new BadRequestException("INVALID_PUSH_PLATFORM", "Push platform must be either fcm or apns.");
        }

        var normalizedDeviceId = string.IsNullOrWhiteSpace(request.DeviceId) ? null : request.DeviceId.Trim();
        var normalizedToken = request.DeviceToken.Trim();

        var device = await _context.UserPushDevices
            .FirstOrDefaultAsync(
                x => x.DeviceToken == normalizedToken
                     || (normalizedDeviceId != null && x.UserId == request.UserId && x.DeviceId == normalizedDeviceId),
                cancellationToken);

        if (device is null)
        {
            device = new UserPushDevice(
                request.UserId,
                normalizedToken,
                platform,
                normalizedDeviceId,
                request.DeviceName,
                request.AppVersion,
                request.Locale,
                request.NotificationsEnabled);

            _context.UserPushDevices.Add(device);
        }
        else
        {
            device.Register(
                request.UserId,
                normalizedToken,
                platform,
                normalizedDeviceId,
                request.DeviceName,
                request.AppVersion,
                request.Locale,
                request.NotificationsEnabled);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Map(device);
    }

    private static NotificationDeviceDto Map(UserPushDevice device) =>
        new(
            device.Id,
            device.DeviceToken,
            device.Platform.ToString().ToLowerInvariant(),
            device.DeviceId,
            device.DeviceName,
            device.AppVersion,
            device.Locale,
            device.NotificationsEnabled,
            device.IsActive,
            device.LastRegisteredAtUtc,
            device.LastSeenAtUtc);
}

public record UpdateNotificationDevicePreferencesCommand(
    Guid UserId,
    string? DeviceId,
    string? DeviceToken,
    bool NotificationsEnabled) : IRequest<NotificationDeviceDto>;

public class UpdateNotificationDevicePreferencesCommandHandler : IRequestHandler<UpdateNotificationDevicePreferencesCommand, NotificationDeviceDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateNotificationDevicePreferencesCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<NotificationDeviceDto> Handle(UpdateNotificationDevicePreferencesCommand request, CancellationToken cancellationToken)
    {
        var device = await NotificationDeviceCommandHelpers.FindOwnedDeviceAsync(
            _context,
            request.UserId,
            request.DeviceId,
            request.DeviceToken,
            cancellationToken);
        device.UpdateNotificationsEnabled(request.NotificationsEnabled);
        await _context.SaveChangesAsync(cancellationToken);
        return new NotificationDeviceDto(
            device.Id,
            device.DeviceToken,
            device.Platform.ToString().ToLowerInvariant(),
            device.DeviceId,
            device.DeviceName,
            device.AppVersion,
            device.Locale,
            device.NotificationsEnabled,
            device.IsActive,
            device.LastRegisteredAtUtc,
            device.LastSeenAtUtc);
    }
}

public record UnregisterNotificationDeviceCommand(
    Guid UserId,
    string? DeviceId,
    string? DeviceToken) : IRequest<int>;

public class UnregisterNotificationDeviceCommandHandler : IRequestHandler<UnregisterNotificationDeviceCommand, int>
{
    private readonly IApplicationDbContext _context;

    public UnregisterNotificationDeviceCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<int> Handle(UnregisterNotificationDeviceCommand request, CancellationToken cancellationToken)
    {
        var devices = await NotificationDeviceCommandHelpers.FindOwnedDevicesAsync(
            _context,
            request.UserId,
            request.DeviceId,
            request.DeviceToken,
            cancellationToken);

        foreach (var device in devices)
        {
            device.Deactivate();
        }

        await _context.SaveChangesAsync(cancellationToken);
        return devices.Count;
    }
}

internal static class NotificationDeviceCommandHelpers
{
    internal static async Task<UserPushDevice> FindOwnedDeviceAsync(
        IApplicationDbContext context,
        Guid userId,
        string? deviceId,
        string? deviceToken,
        CancellationToken cancellationToken)
    {
        var devices = await FindOwnedDevicesAsync(context, userId, deviceId, deviceToken, cancellationToken);
        var lookupKey = (object?)deviceId ?? deviceToken ?? userId.ToString();
        return devices.FirstOrDefault() ?? throw new NotFoundException("NotificationDevice", lookupKey);
    }

    internal static async Task<List<UserPushDevice>> FindOwnedDevicesAsync(
        IApplicationDbContext context,
        Guid userId,
        string? deviceId,
        string? deviceToken,
        CancellationToken cancellationToken)
    {
        var normalizedDeviceId = string.IsNullOrWhiteSpace(deviceId) ? null : deviceId.Trim();
        var normalizedToken = string.IsNullOrWhiteSpace(deviceToken) ? null : deviceToken.Trim();

        if (normalizedDeviceId is null && normalizedToken is null)
        {
            throw new BadRequestException("DEVICE_IDENTIFIER_REQUIRED", "DeviceId or device token is required.");
        }

        return await context.UserPushDevices
            .Where(x => x.UserId == userId
                        && ((normalizedDeviceId != null && x.DeviceId == normalizedDeviceId)
                            || (normalizedToken != null && x.DeviceToken == normalizedToken)))
            .ToListAsync(cancellationToken);
    }
}
