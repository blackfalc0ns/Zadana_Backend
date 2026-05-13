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
    bool DispatchPushEnabled,
    bool AssignmentPushEnabled,
    bool SupportPushEnabled,
    bool WalletPushEnabled,
    bool AccountPushEnabled,
    bool AdminDriversPushEnabled,
    bool AdminVendorsPushEnabled,
    bool AdminCatalogPushEnabled,
    bool AdminDisputesPushEnabled,
    bool AdminRefundsPushEnabled,
    bool AdminSettlementsPushEnabled,
    bool AdminSupportPushEnabled,
    bool AdminSystemPushEnabled,
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
    bool NotificationsEnabled = true,
    bool DispatchPushEnabled = true,
    bool AssignmentPushEnabled = true,
    bool SupportPushEnabled = true,
    bool WalletPushEnabled = true,
    bool AccountPushEnabled = true,
    bool AdminDriversPushEnabled = true,
    bool AdminVendorsPushEnabled = true,
    bool AdminCatalogPushEnabled = true,
    bool AdminDisputesPushEnabled = true,
    bool AdminRefundsPushEnabled = true,
    bool AdminSettlementsPushEnabled = true,
    bool AdminSupportPushEnabled = true,
    bool AdminSystemPushEnabled = true) : IRequest<NotificationDeviceDto>;

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
            throw new BadRequestException("INVALID_PUSH_PLATFORM", "Push platform must be fcm, apns, or web.");
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
                request.NotificationsEnabled,
                request.DispatchPushEnabled,
                request.AssignmentPushEnabled,
                request.SupportPushEnabled,
                request.WalletPushEnabled,
                request.AccountPushEnabled,
                request.AdminDriversPushEnabled,
                request.AdminVendorsPushEnabled,
                request.AdminCatalogPushEnabled,
                request.AdminDisputesPushEnabled,
                request.AdminRefundsPushEnabled,
                request.AdminSettlementsPushEnabled,
                request.AdminSupportPushEnabled,
                request.AdminSystemPushEnabled);

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
                request.NotificationsEnabled,
                request.DispatchPushEnabled,
                request.AssignmentPushEnabled,
                request.SupportPushEnabled,
                request.WalletPushEnabled,
                request.AccountPushEnabled,
                request.AdminDriversPushEnabled,
                request.AdminVendorsPushEnabled,
                request.AdminCatalogPushEnabled,
                request.AdminDisputesPushEnabled,
                request.AdminRefundsPushEnabled,
                request.AdminSettlementsPushEnabled,
                request.AdminSupportPushEnabled,
                request.AdminSystemPushEnabled);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Map(device);
    }

    internal static NotificationDeviceDto Map(UserPushDevice device) =>
        new(
            device.Id,
            device.DeviceToken,
            device.Platform.ToString().ToLowerInvariant(),
            device.DeviceId,
            device.DeviceName,
            device.AppVersion,
            device.Locale,
            device.NotificationsEnabled,
            device.DispatchPushEnabled,
            device.AssignmentPushEnabled,
            device.SupportPushEnabled,
            device.WalletPushEnabled,
            device.AccountPushEnabled,
            device.AdminDriversPushEnabled,
            device.AdminVendorsPushEnabled,
            device.AdminCatalogPushEnabled,
            device.AdminDisputesPushEnabled,
            device.AdminRefundsPushEnabled,
            device.AdminSettlementsPushEnabled,
            device.AdminSupportPushEnabled,
            device.AdminSystemPushEnabled,
            device.IsActive,
            device.LastRegisteredAtUtc,
            device.LastSeenAtUtc);
}

public record UpdateNotificationDevicePreferencesCommand(
    Guid UserId,
    string? DeviceId,
    string? DeviceToken,
    bool NotificationsEnabled,
    bool? DispatchPushEnabled = null,
    bool? AssignmentPushEnabled = null,
    bool? SupportPushEnabled = null,
    bool? WalletPushEnabled = null,
    bool? AccountPushEnabled = null,
    bool? AdminDriversPushEnabled = null,
    bool? AdminVendorsPushEnabled = null,
    bool? AdminCatalogPushEnabled = null,
    bool? AdminDisputesPushEnabled = null,
    bool? AdminRefundsPushEnabled = null,
    bool? AdminSettlementsPushEnabled = null,
    bool? AdminSupportPushEnabled = null,
    bool? AdminSystemPushEnabled = null) : IRequest<NotificationDeviceDto>;

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
        device.UpdatePushPreferences(
            request.NotificationsEnabled,
            request.DispatchPushEnabled,
            request.AssignmentPushEnabled,
            request.SupportPushEnabled,
            request.WalletPushEnabled,
            request.AccountPushEnabled,
            request.AdminDriversPushEnabled,
            request.AdminVendorsPushEnabled,
            request.AdminCatalogPushEnabled,
            request.AdminDisputesPushEnabled,
            request.AdminRefundsPushEnabled,
            request.AdminSettlementsPushEnabled,
            request.AdminSupportPushEnabled,
            request.AdminSystemPushEnabled);
        await _context.SaveChangesAsync(cancellationToken);
        return RegisterNotificationDeviceCommandHandler.Map(device);
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
