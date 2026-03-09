using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;
using Zadana.Application.Common.Interfaces;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Infrastructure.Services;

public class TwilioOtpService : IOtpService
{
    private readonly TwilioSettings _settings;
    private readonly ILogger<TwilioOtpService> _logger;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public TwilioOtpService(
        IOptions<TwilioSettings> settings,
        ILogger<TwilioOtpService> logger,
        IStringLocalizer<SharedResource> localizer)
    {
        _settings = settings.Value;
        _logger = logger;
        _localizer = localizer;

        TwilioClient.Init(_settings.AccountSid, _settings.AuthToken);
    }

    public async Task SendOtpSmsAsync(string phoneNumber, string otpCode, CancellationToken cancellationToken = default)
    {
        try
        {
            // Ensure phone number starts with country code
            var formattedPhone = FormatPhoneNumber(phoneNumber);

            var message = await MessageResource.CreateAsync(
                to: new PhoneNumber(formattedPhone),
                from: new PhoneNumber(_settings.FromNumber),
                body: _localizer["OtpSmsMessage", otpCode].Value
            );

            _logger.LogInformation("SMS OTP sent successfully to {Phone}. SID: {Sid}", formattedPhone, message.Sid);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Failed to send SMS OTP to {Phone}. Registration will continue without SMS delivery.", phoneNumber);
            // Don't throw - registration should succeed even if SMS fails (e.g., Twilio Trial restrictions)
        }
    }

    public async Task SendOtpEmailAsync(string emailAddress, string otpCode, CancellationToken cancellationToken = default)
    {
        // Twilio SMS service - email OTP is handled by the Resend email service separately
        _logger.LogInformation("Email OTP for {Email} is handled by the email service. Code: {Code}", emailAddress, otpCode);
        await Task.CompletedTask;
    }

    /// <summary>
    /// Formats Egyptian phone numbers (starting with 0) to international format (+20).
    /// </summary>
    private static string FormatPhoneNumber(string phone)
    {
        if (phone.StartsWith("0"))
            return "+20" + phone[1..];

        if (!phone.StartsWith("+"))
            return "+20" + phone;

        return phone;
    }
}
