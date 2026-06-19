using Zadana.Application.Common.Interfaces;
using Zadana.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace Zadana.Infrastructure.Services;

public sealed class CompositeOtpService : IOtpService
{
    private readonly WhatsAppCloudOtpService _cloudOtpService;
    private readonly WapilotWhatsAppOtpService _whatsAppOtpService;
    private readonly EmailOtpService _emailOtpService;
    private readonly WhatsAppCloudOtpSettings _cloudSettings;

    public CompositeOtpService(
        WhatsAppCloudOtpService cloudOtpService,
        WapilotWhatsAppOtpService whatsAppOtpService,
        EmailOtpService emailOtpService,
        IOptions<WhatsAppCloudOtpSettings> cloudSettings)
    {
        _cloudOtpService = cloudOtpService;
        _whatsAppOtpService = whatsAppOtpService;
        _emailOtpService = emailOtpService;
        _cloudSettings = cloudSettings.Value;
    }

    public Task SendOtpSmsAsync(string phoneNumber, string otpCode, CancellationToken cancellationToken = default) =>
        _cloudSettings.Enabled
            ? _cloudOtpService.SendOtpSmsAsync(phoneNumber, otpCode, cancellationToken)
            : _whatsAppOtpService.SendOtpSmsAsync(phoneNumber, otpCode, cancellationToken);

    public Task SendOtpEmailAsync(
        string emailAddress,
        string otpCode,
        CancellationToken cancellationToken = default,
        int validityMinutes = 5) =>
        _emailOtpService.SendOtpEmailAsync(emailAddress, otpCode, cancellationToken, validityMinutes);
}
