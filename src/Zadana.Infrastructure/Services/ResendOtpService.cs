using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zadana.Application.Common.Interfaces;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;
using Zadana.Infrastructure.Email;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Infrastructure.Services;

public class ResendOtpService : IOtpService
{
    private readonly IEmailService _emailService;
    private readonly ILogger<ResendOtpService> _logger;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly ITemplateService _templateService;
    private readonly ResendEmailSettings _emailSettings;

    public ResendOtpService(
        IEmailService emailService, 
        ILogger<ResendOtpService> logger,
        IStringLocalizer<SharedResource> localizer,
        ITemplateService templateService,
        IOptions<ResendEmailSettings> emailSettings)
    {
        _emailService = emailService;
        _logger = logger;
        _localizer = localizer;
        _templateService = templateService;
        _emailSettings = emailSettings.Value;
    }

    public async Task SendOtpEmailAsync(
        string emailAddress,
        string otpCode,
        CancellationToken cancellationToken = default,
        int validityMinutes = 5)
    {
        try
        {
            var subject = _localizer["OtpEmailSubject"].Value;
            var placeholders = new Dictionary<string, string>
            {
                { "OtpCode", otpCode },
                { "EmailAddress", emailAddress },
                { "SupportEmail", _emailSettings.SupportEmail },
                { "LogoUrl", _emailSettings.LogoUrl },
                { "OtpHeroImageUrl", _emailSettings.OtpHeroImageUrl },
                { "ValidityMinutes", validityMinutes.ToString(CultureInfo.InvariantCulture) },
                { "Year", DateTime.UtcNow.Year.ToString() }
            };
            var body = await _templateService.RenderTemplateAsync("OtpEmail", placeholders);
            var isArabic = CultureInfo.CurrentCulture.Name.StartsWith("ar", StringComparison.OrdinalIgnoreCase);
            var textBody = isArabic
                ? $"رمز التحقق من زادنا هو {otpCode}. هذا الرمز صالح لمدة {validityMinutes} دقائق فقط. تم إرسال هذا الرمز إلى {emailAddress} لتأكيد حسابك. لا تشاركه مع أي شخص. إذا لم تطلب هذا الرمز، تواصل معنا على {_emailSettings.SupportEmail}."
                : $"Your Zadna verification code is {otpCode}. This code is valid for {validityMinutes} minutes only. It was sent to {emailAddress} to confirm your account. Do not share it with anyone. If you did not request this code, contact {_emailSettings.SupportEmail}.";

            var result = await _emailService.SendEmailAsync(
                new SendEmailRequest(
                    [emailAddress],
                    subject,
                    body,
                    TextBody: textBody,
                    From: $"{_emailSettings.FromName} Support <{_emailSettings.SupportEmail}>",
                    ReplyTo: _emailSettings.SupportEmail,
                    Headers: new Dictionary<string, string>
                    {
                        ["X-Entity-Ref-ID"] = $"zadna-otp-{Guid.NewGuid():N}"
                    }),
                cancellationToken);

            if (!result.Success)
            {
                throw new ExternalServiceException(
                    "RESEND_OTP_EMAIL_FAILED",
                    result.FailureReason ?? "OTP email delivery failed.");
            }

            _logger.LogInformation("Email OTP sent successfully to {Email}", emailAddress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email OTP to {Email}", emailAddress);
            throw;
        }
    }

    public Task SendOtpSmsAsync(string phoneNumber, string otpCode, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("SMS OTP is requested but not implemented in ResendOtpService. Use TwilioOtpService if SMS is needed.");
        return Task.CompletedTask;
    }
}
