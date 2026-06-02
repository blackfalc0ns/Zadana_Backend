using Zadana.Application.Common.Interfaces;

namespace Zadana.Infrastructure.Services;

public sealed class CompositeOtpService : IOtpService
{
    private readonly WapilotWhatsAppOtpService _whatsAppOtpService;
    private readonly ResendOtpService _emailOtpService;

    public CompositeOtpService(
        WapilotWhatsAppOtpService whatsAppOtpService,
        ResendOtpService emailOtpService)
    {
        _whatsAppOtpService = whatsAppOtpService;
        _emailOtpService = emailOtpService;
    }

    public Task SendOtpSmsAsync(string phoneNumber, string otpCode, CancellationToken cancellationToken = default) =>
        _whatsAppOtpService.SendOtpSmsAsync(phoneNumber, otpCode, cancellationToken);

    public Task SendOtpEmailAsync(
        string emailAddress,
        string otpCode,
        CancellationToken cancellationToken = default,
        int validityMinutes = 5) =>
        _emailOtpService.SendOtpEmailAsync(emailAddress, otpCode, cancellationToken, validityMinutes);
}
