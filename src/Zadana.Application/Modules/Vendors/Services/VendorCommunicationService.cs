using System.Text.Json;
using Microsoft.Extensions.Logging;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.EmailCenter.Interfaces;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Domain.Modules.Vendors.Entities;

namespace Zadana.Application.Modules.Vendors.Services;

public sealed class VendorCommunicationService : IVendorCommunicationService
{
    private readonly INotificationService _notificationService;
    private readonly IOneSignalPushService _oneSignalPushService;
    private readonly IEmailCenterService _emailCenterService;
    private readonly ILogger<VendorCommunicationService> _logger;

    public VendorCommunicationService(
        INotificationService notificationService,
        IOneSignalPushService oneSignalPushService,
        IEmailCenterService emailCenterService,
        ILogger<VendorCommunicationService> logger)
    {
        _notificationService = notificationService;
        _oneSignalPushService = oneSignalPushService;
        _emailCenterService = emailCenterService;
        _logger = logger;
    }

    public async Task<VendorCommunicationDispatchResult> SendAsync(
        Vendor vendor,
        VendorCommunicationMessage message,
        CancellationToken cancellationToken = default)
    {
        var data = BuildData(vendor, message);

        if (message.SendInbox)
        {
            await _notificationService.SendToUserAsync(
                vendor.UserId,
                message.TitleAr,
                message.TitleEn,
                message.BodyAr,
                message.BodyEn,
                message.Type,
                message.ReferenceId,
                data,
                cancellationToken);
        }

        var pushResult = message.SendPush
            ? await _oneSignalPushService.SendToExternalUserAsync(
                vendor.UserId.ToString(),
                message.TitleAr,
                message.TitleEn,
                message.BodyAr,
                message.BodyEn,
                message.Type,
                message.ReferenceId,
                data,
                message.TargetUrl,
                cancellationToken)
            : new OneSignalPushDispatchResult(
                Attempted: false,
                Sent: false,
                Skipped: true,
                ProviderStatusCode: null,
                ProviderNotificationId: null,
                Reason: "Push dispatch was disabled for this vendor communication.");

        var (emailAttempted, emailSent, emailSkipped, emailReason) = await SendEmailAsync(vendor, message, cancellationToken);

        return new VendorCommunicationDispatchResult(
            message.SendInbox,
            pushResult.Attempted,
            pushResult.Sent,
            pushResult.Skipped,
            pushResult.ProviderStatusCode,
            pushResult.ProviderNotificationId,
            pushResult.Reason,
            emailAttempted,
            emailSent,
            emailSkipped,
            emailReason);
    }

    private async Task<(bool Attempted, bool Sent, bool Skipped, string? Reason)> SendEmailAsync(
        Vendor vendor,
        VendorCommunicationMessage message,
        CancellationToken cancellationToken)
    {
        if (!message.SendEmail)
        {
            return (false, false, true, "Email dispatch was disabled for this vendor communication.");
        }

        if (!vendor.EmailNotificationsEnabled)
        {
            return (false, false, true, "Vendor email notifications are disabled.");
        }

        try
        {
            var result = await _emailCenterService.DispatchVendorEmailAsync(vendor, message, cancellationToken);
            return (result.Attempted, result.Sent, result.Skipped, result.Reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send vendor lifecycle email for vendor {VendorId}", vendor.Id);
            return (true, false, false, ex.Message);
        }
    }

    private static string BuildData(Vendor vendor, VendorCommunicationMessage message)
    {
        if (!string.IsNullOrWhiteSpace(message.Data))
        {
            return message.Data;
        }

        return JsonSerializer.Serialize(new
        {
            vendorId = vendor.Id,
            userId = vendor.UserId,
            targetUrl = message.TargetUrl,
            source = "vendor_lifecycle",
            generatedAtUtc = DateTime.UtcNow
        });
    }
}
