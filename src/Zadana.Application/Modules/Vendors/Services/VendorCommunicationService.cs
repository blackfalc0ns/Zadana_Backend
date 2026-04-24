using System.Text.Json;
using Microsoft.Extensions.Logging;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Domain.Modules.Vendors.Entities;

namespace Zadana.Application.Modules.Vendors.Services;

public sealed class VendorCommunicationService : IVendorCommunicationService
{
    private readonly INotificationService _notificationService;
    private readonly IOneSignalPushService _oneSignalPushService;
    private readonly IEmailService _emailService;
    private readonly ILogger<VendorCommunicationService> _logger;

    public VendorCommunicationService(
        INotificationService notificationService,
        IOneSignalPushService oneSignalPushService,
        IEmailService emailService,
        ILogger<VendorCommunicationService> logger)
    {
        _notificationService = notificationService;
        _oneSignalPushService = oneSignalPushService;
        _emailService = emailService;
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

        var to = ResolveVendorEmail(vendor);
        if (string.IsNullOrWhiteSpace(to))
        {
            return (false, false, true, "Vendor has no email address for lifecycle communication.");
        }

        try
        {
            await _emailService.SendEmailAsync(
                to,
                message.TitleEn,
                BuildEmailBody(vendor, message),
                cancellationToken);

            return (true, true, false, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send vendor lifecycle email to {Email} for vendor {VendorId}", to, vendor.Id);
            return (true, false, false, ex.Message);
        }
    }

    private static string ResolveVendorEmail(Vendor vendor) =>
        !string.IsNullOrWhiteSpace(vendor.OwnerEmail)
            ? vendor.OwnerEmail!
            : vendor.ContactEmail;

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

    private static string BuildEmailBody(Vendor vendor, VendorCommunicationMessage message)
    {
        var vendorName = string.IsNullOrWhiteSpace(vendor.BusinessNameEn)
            ? vendor.BusinessNameAr
            : vendor.BusinessNameEn;

        return $"""
            <div style="font-family:Arial,sans-serif;line-height:1.7;color:#0f172a">
              <h2 style="margin:0 0 12px;color:#0f766e">{message.TitleEn}</h2>
              <p>Hello {vendorName},</p>
              <p>{message.BodyEn}</p>
              <hr style="border:none;border-top:1px solid #e2e8f0;margin:20px 0" />
              <div dir="rtl" style="font-family:Tahoma,Arial,sans-serif">
                <h3 style="margin:0 0 8px;color:#0f766e">{message.TitleAr}</h3>
                <p>{message.BodyAr}</p>
              </div>
              <p style="margin-top:20px">
                <a href="{message.TargetUrl}" style="display:inline-block;background:#0f766e;color:#fff;text-decoration:none;padding:10px 16px;border-radius:12px">
                  Open vendor panel
                </a>
              </p>
            </div>
            """;
    }
}
