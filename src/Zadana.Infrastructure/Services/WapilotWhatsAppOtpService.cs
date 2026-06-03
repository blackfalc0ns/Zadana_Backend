using System.Globalization;
using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zadana.Application.Common.Interfaces;
using Zadana.Infrastructure.Settings;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Infrastructure.Services;

public sealed class WapilotWhatsAppOtpService : IOtpService
{
    private readonly HttpClient _httpClient;
    private readonly WapilotOtpSettings _settings;
    private readonly ILogger<WapilotWhatsAppOtpService> _logger;

    public WapilotWhatsAppOtpService(
        HttpClient httpClient,
        IOptions<WapilotOtpSettings> settings,
        ILogger<WapilotWhatsAppOtpService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendOtpSmsAsync(string phoneNumber, string otpCode, CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            _logger.LogWarning("WAPIlot WhatsApp OTP is disabled. OTP delivery was skipped for {Phone}.", MaskPhone(phoneNumber));
            return;
        }

        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            throw new ExternalServiceException(
                "WAPILOT_OTP_NOT_CONFIGURED",
                "WAPIlot WhatsApp OTP is enabled but no API key is configured.");
        }

        if (string.IsNullOrWhiteSpace(_settings.InstanceId))
        {
            throw new ExternalServiceException(
                "WAPILOT_OTP_NOT_CONFIGURED",
                "WAPIlot WhatsApp OTP is enabled but no instance ID is configured.");
        }

        string formattedPhone;
        try
        {
            formattedPhone = WhatsAppPhoneNumberNormalizer.Normalize(phoneNumber, _settings.DefaultCountryCode);
        }
        catch (ArgumentException)
        {
            throw new BadRequestException(
                "INVALID_WHATSAPP_PHONE_NUMBER",
                "Phone number is not valid for WhatsApp OTP delivery.");
        }

        var chatId = ToWapilotChatId(formattedPhone);
        var message = BuildOtpMessage(otpCode);
        using var request = new HttpRequestMessage(HttpMethod.Post, ResolveSendPath())
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["chat_id"] = chatId,
                ["text"] = message
            })
        };
        request.Headers.TryAddWithoutValidation("token", _settings.ApiKey.Trim());

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("WhatsApp OTP sent successfully via WAPIlot to {Phone}.", MaskPhone(formattedPhone));
                return;
            }

            var status = response.StatusCode;
            _logger.LogWarning(
                "WAPIlot WhatsApp OTP delivery failed for {Phone}. StatusCode={StatusCode}",
                MaskPhone(formattedPhone),
                (int)status);

            throw new ExternalServiceException(
                ResolveErrorCode(status),
                ResolveFailureMessage(status));
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ExternalServiceException(
                "WAPILOT_WHATSAPP_OTP_TIMEOUT",
                "WAPIlot WhatsApp OTP delivery timed out.",
                ex);
        }
        catch (HttpRequestException ex)
        {
            throw new ExternalServiceException(
                "WAPILOT_WHATSAPP_OTP_UNAVAILABLE",
                "WAPIlot WhatsApp OTP service is unavailable.",
                ex);
        }
    }

    public Task SendOtpEmailAsync(
        string emailAddress,
        string otpCode,
        CancellationToken cancellationToken = default,
        int validityMinutes = 5)
    {
        _logger.LogDebug("WAPIlot WhatsApp OTP service does not send email OTP.");
        return Task.CompletedTask;
    }

    private string BuildOtpMessage(string otpCode)
    {
        var isArabic = CultureInfo.CurrentUICulture.Name.StartsWith("ar", StringComparison.OrdinalIgnoreCase);
        var template = isArabic ? _settings.MessageTemplateAr : _settings.MessageTemplateEn;
        if (string.IsNullOrWhiteSpace(template))
        {
            template = isArabic
                ? "رمز تحقق زادنا:\n```{0}```\n\nلا تشارك هذا الرمز مع أي شخص."
                : "ZADANA verification code:\n```{0}```\n\nDo not share this code with anyone.";
        }

        return template.Contains("{0}", StringComparison.Ordinal)
            ? template.Replace("{0}", otpCode, StringComparison.Ordinal)
            : $"{template.Trim()} {otpCode}";
    }

    private string ResolveSendPath()
    {
        var path = string.IsNullOrWhiteSpace(_settings.SendMessagePath)
            ? "/api/v2/{instance_id}/send-message"
            : _settings.SendMessagePath.Trim();

        path = path.Replace("{instance_id}", Uri.EscapeDataString(_settings.InstanceId.Trim()), StringComparison.OrdinalIgnoreCase);
        return path.StartsWith("/", StringComparison.Ordinal) ? path : "/" + path;
    }

    private static string ToWapilotChatId(string formattedPhone)
    {
        var digits = new string(formattedPhone.Where(char.IsDigit).ToArray());
        return $"{digits}@c.us";
    }

    private static string ResolveErrorCode(HttpStatusCode statusCode) =>
        statusCode switch
        {
            HttpStatusCode.Unauthorized => "WAPILOT_INVALID_API_KEY",
            HttpStatusCode.Forbidden => "WAPILOT_INSTANCE_SUSPENDED",
            HttpStatusCode.TooManyRequests => "WAPILOT_RATE_LIMITED",
            _ => "WAPILOT_WHATSAPP_OTP_FAILED"
        };

    private static string ResolveFailureMessage(HttpStatusCode statusCode) =>
        statusCode switch
        {
            HttpStatusCode.Unauthorized => "WAPIlot WhatsApp OTP API key is invalid.",
            HttpStatusCode.Forbidden => "WAPIlot WhatsApp OTP instance is suspended.",
            HttpStatusCode.TooManyRequests => "WAPIlot WhatsApp OTP rate limit has been exceeded.",
            _ => "WAPIlot WhatsApp OTP delivery failed."
        };

    private static string MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return "***";
        }

        var compact = new string(phone.Where(ch => !char.IsWhiteSpace(ch)).ToArray());
        if (compact.Length <= 4)
        {
            return new string('*', compact.Length);
        }

        return string.Concat(new string('*', compact.Length - 4), compact.AsSpan(compact.Length - 4));
    }

}
