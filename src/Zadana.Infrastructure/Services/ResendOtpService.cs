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
            var isArabic = CultureInfo.CurrentCulture.Name.StartsWith("ar", StringComparison.OrdinalIgnoreCase);
            var isPasswordReset = validityMinutes >= 15;
            var subject = ResolveOtpEmailSubject(isArabic, isPasswordReset);
            var placeholders = new Dictionary<string, string>
            {
                { "OtpCode", otpCode },
                { "EmailAddress", emailAddress },
                { "SupportEmail", _emailSettings.SupportEmail },
                { "LogoUrl", _emailSettings.LogoUrl },
                { "OtpHeroImageUrl", ResolveOtpHeroImageUrl(isArabic, validityMinutes) },
                { "ValidityMinutes", validityMinutes.ToString(CultureInfo.InvariantCulture) },
                { "Year", DateTime.UtcNow.Year.ToString(CultureInfo.InvariantCulture) }
            };

            foreach (var item in BuildOtpTemplateCopy(isArabic, isPasswordReset, otpCode, validityMinutes))
            {
                placeholders[item.Key] = item.Value;
            }

            var body = await _templateService.RenderTemplateAsync("OtpEmail", placeholders);
            var textBody = BuildTextBody(isArabic, isPasswordReset, emailAddress, otpCode, validityMinutes, _emailSettings.SupportEmail);

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

    private string ResolveOtpEmailSubject(bool isArabic, bool isPasswordReset)
    {
        if (!isPasswordReset)
        {
            return _localizer["OtpEmailSubject"].Value;
        }

        return isArabic
            ? "رمز إعادة تعيين كلمة السر من زادنا"
            : "Reset your Zadna password";
    }

    private static string BuildTextBody(
        bool isArabic,
        bool isPasswordReset,
        string emailAddress,
        string otpCode,
        int validityMinutes,
        string supportEmail)
    {
        if (isArabic)
        {
            return isPasswordReset
                ? $"رمز إعادة تعيين كلمة السر من زادنا هو {otpCode}. هذا الرمز صالح لمدة {validityMinutes} دقيقة فقط. تم إرسال هذا الرمز إلى {emailAddress}. لا تشاركه مع أي شخص. إذا لم تطلب إعادة التعيين، تواصل معنا على {supportEmail}."
                : $"رمز التحقق من زادنا هو {otpCode}. هذا الرمز صالح لمدة {validityMinutes} دقائق فقط. تم إرسال هذا الرمز إلى {emailAddress}. لا تشاركه مع أي شخص. إذا لم تطلب هذا الرمز، تواصل معنا على {supportEmail}.";
        }

        return isPasswordReset
            ? $"Your Zadna password reset code is {otpCode}. This code is valid for {validityMinutes} minutes only. It was sent to {emailAddress}. Do not share it with anyone. If you did not request this reset, contact {supportEmail}."
            : $"Your Zadna verification code is {otpCode}. This code is valid for {validityMinutes} minutes only. It was sent to {emailAddress}. Do not share it with anyone. If you did not request this code, contact {supportEmail}.";
    }

    private static Dictionary<string, string> BuildOtpTemplateCopy(
        bool isArabic,
        bool isPasswordReset,
        string otpCode,
        int validityMinutes)
    {
        if (isArabic)
        {
            return isPasswordReset
                ? new Dictionary<string, string>
                {
                    ["OtpPreheader"] = $"رمز إعادة تعيين كلمة السر من زادنا هو {otpCode}.",
                    ["OtpEyebrow"] = "رمز أمان",
                    ["OtpTitle"] = "إعادة تعيين كلمة السر",
                    ["OtpGreeting"] = "مرحباً،",
                    ["OtpInstruction"] = "استخدم رمز الأمان التالي لإعادة تعيين كلمة السر الخاصة بحسابك في زادنا.",
                    ["OtpCodeLabel"] = "رمز إعادة التعيين",
                    ["OtpExpiryNote"] = $"هذا الرمز صالح لمدة {validityMinutes} دقيقة فقط.",
                    ["OtpUsageNote"] = "لا تشارك هذا الرمز مع أي شخص. استخدمه فقط داخل زادنا لإكمال إعادة تعيين كلمة السر.",
                    ["OtpSecurityNote"] = "زادنا لن تطلب منك إرسال هذا الرمز عبر الهاتف أو المحادثة أو البريد.",
                    ["OtpNotice"] = "إذا لم تطلب إعادة تعيين كلمة السر، تجاهل هذه الرسالة أو تواصل مع دعم زادنا.",
                    ["OtpFooter"] = $"Copyright {DateTime.UtcNow.Year} Zadna. رسالة أمان تلقائية."
                }
                : new Dictionary<string, string>
                {
                    ["OtpPreheader"] = $"رمز التحقق لمرة واحدة من زادنا هو {otpCode}.",
                    ["OtpEyebrow"] = "رمز لمرة واحدة",
                    ["OtpTitle"] = "أكد حسابك في زادنا",
                    ["OtpGreeting"] = "مرحباً،",
                    ["OtpInstruction"] = "استخدم رمز التحقق التالي لإكمال تأكيد حسابك في زادنا بأمان.",
                    ["OtpCodeLabel"] = "رمز التحقق",
                    ["OtpExpiryNote"] = $"هذا الرمز صالح لمدة {validityMinutes} دقائق فقط.",
                    ["OtpUsageNote"] = "هذا الرمز صالح لفترة قصيرة ولا يستخدم إلا داخل زادنا. لا تشاركه مع أي شخص.",
                    ["OtpSecurityNote"] = "زادنا لن تطلب منك إرسال هذا الرمز عبر الهاتف أو المحادثة أو البريد.",
                    ["OtpNotice"] = "إذا لم تطلب هذا الرمز، يمكنك تجاهل هذه الرسالة أو التواصل مع دعم زادنا.",
                    ["OtpFooter"] = $"Copyright {DateTime.UtcNow.Year} Zadna. رسالة أمان تلقائية."
                };
        }

        return isPasswordReset
            ? new Dictionary<string, string>
            {
                ["OtpPreheader"] = $"Your Zadna password reset code is {otpCode}.",
                ["OtpEyebrow"] = "Security code",
                ["OtpTitle"] = "Reset your password",
                ["OtpGreeting"] = "Hello,",
                ["OtpInstruction"] = "Use the security code below to reset your Zadna account password.",
                ["OtpCodeLabel"] = "Reset code",
                ["OtpExpiryNote"] = $"This code is valid for {validityMinutes} minutes only.",
                ["OtpUsageNote"] = "Use this code only inside Zadna to complete your password reset. Do not share it with anyone.",
                ["OtpSecurityNote"] = "Zadna will never ask you to send this code by phone, chat, or email.",
                ["OtpNotice"] = "If you did not request a password reset, ignore this email or contact Zadna Support.",
                ["OtpFooter"] = $"Copyright {DateTime.UtcNow.Year} Zadna. This is an automated security message."
            }
            : new Dictionary<string, string>
            {
                ["OtpPreheader"] = $"Your Zadna one-time verification code is {otpCode}.",
                ["OtpEyebrow"] = "One-time code",
                ["OtpTitle"] = "Confirm your Zadna account",
                ["OtpGreeting"] = "Hello,",
                ["OtpInstruction"] = "Use the verification code below to complete your account confirmation securely.",
                ["OtpCodeLabel"] = "Verification code",
                ["OtpExpiryNote"] = $"This code is valid for {validityMinutes} minutes only.",
                ["OtpUsageNote"] = "This code expires shortly and can only be used inside Zadna. Do not share it with anyone.",
                ["OtpSecurityNote"] = "Zadna will never ask you to send this code by phone, chat, or email.",
                ["OtpNotice"] = "If you did not request this code, you can ignore this email or contact Zadna Support.",
                ["OtpFooter"] = $"Copyright {DateTime.UtcNow.Year} Zadna. This is an automated security message."
            };
    }

    private string ResolveOtpHeroImageUrl(bool isArabic, int validityMinutes)
    {
        var isPasswordReset = validityMinutes >= 15;
        var localizedUrl = isPasswordReset
            ? isArabic
                ? _emailSettings.PasswordResetHeroImageUrlAr
                : _emailSettings.PasswordResetHeroImageUrlEn
            : isArabic
                ? _emailSettings.OtpHeroImageUrlAr
                : _emailSettings.OtpHeroImageUrlEn;

        return string.IsNullOrWhiteSpace(localizedUrl)
            ? _emailSettings.OtpHeroImageUrl
            : localizedUrl.Trim();
    }
}
