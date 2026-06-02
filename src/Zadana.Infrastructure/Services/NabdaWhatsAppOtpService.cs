using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zadana.Application.Common.Interfaces;
using Zadana.Infrastructure.Settings;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Infrastructure.Services;

public sealed class NabdaWhatsAppOtpService : IOtpService
{
    private const string SendMessagePath = "api/v1/messages/send";

    private readonly HttpClient _httpClient;
    private readonly NabdaOtpSettings _settings;
    private readonly ILogger<NabdaWhatsAppOtpService> _logger;

    public NabdaWhatsAppOtpService(
        HttpClient httpClient,
        IOptions<NabdaOtpSettings> settings,
        ILogger<NabdaWhatsAppOtpService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendOtpSmsAsync(string phoneNumber, string otpCode, CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            _logger.LogWarning("Nabda WhatsApp OTP is disabled. OTP delivery was skipped for {Phone}.", MaskPhone(phoneNumber));
            return;
        }

        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            throw new ExternalServiceException(
                "NABDA_OTP_NOT_CONFIGURED",
                "Nabda WhatsApp OTP is enabled but no API key is configured.");
        }

        string formattedPhone;
        try
        {
            formattedPhone = NabdaPhoneNumberNormalizer.Normalize(phoneNumber, _settings.DefaultCountryCode);
        }
        catch (ArgumentException)
        {
            throw new BadRequestException(
                "INVALID_WHATSAPP_PHONE_NUMBER",
                "Phone number is not valid for WhatsApp OTP delivery.");
        }

        var message = BuildOtpMessage(otpCode);

        using var request = new HttpRequestMessage(HttpMethod.Post, SendMessagePath)
        {
            Content = JsonContent.Create(new NabdaSendMessageRequest(formattedPhone, message))
        };
        request.Headers.TryAddWithoutValidation("Authorization", _settings.ApiKey.Trim());

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("WhatsApp OTP sent successfully via Nabda to {Phone}.", MaskPhone(formattedPhone));
                return;
            }

            var status = response.StatusCode;
            _logger.LogWarning(
                "Nabda WhatsApp OTP delivery failed for {Phone}. StatusCode={StatusCode}",
                MaskPhone(formattedPhone),
                (int)status);

            throw new ExternalServiceException(
                ResolveErrorCode(status),
                ResolveFailureMessage(status));
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ExternalServiceException(
                "NABDA_WHATSAPP_OTP_TIMEOUT",
                "Nabda WhatsApp OTP delivery timed out.",
                ex);
        }
        catch (HttpRequestException ex)
        {
            throw new ExternalServiceException(
                "NABDA_WHATSAPP_OTP_UNAVAILABLE",
                "Nabda WhatsApp OTP service is unavailable.",
                ex);
        }
    }

    public Task SendOtpEmailAsync(
        string emailAddress,
        string otpCode,
        CancellationToken cancellationToken = default,
        int validityMinutes = 5)
    {
        _logger.LogDebug("Nabda WhatsApp OTP service does not send email OTP.");
        return Task.CompletedTask;
    }

    private string BuildOtpMessage(string otpCode)
    {
        var isArabic = CultureInfo.CurrentUICulture.Name.StartsWith("ar", StringComparison.OrdinalIgnoreCase);
        var template = isArabic ? _settings.MessageTemplateAr : _settings.MessageTemplateEn;
        if (string.IsNullOrWhiteSpace(template))
        {
            template = isArabic
                ? "رمز التحقق من زادنا هو {0}. لا تشاركه مع أي شخص."
                : "Your Zadana verification code is {0}. Do not share it with anyone.";
        }

        return template.Contains("{0}", StringComparison.Ordinal)
            ? template.Replace("{0}", otpCode, StringComparison.Ordinal)
            : $"{template.Trim()} {otpCode}";
    }

    private static string ResolveErrorCode(HttpStatusCode statusCode) =>
        statusCode switch
        {
            HttpStatusCode.Unauthorized => "NABDA_INVALID_API_KEY",
            HttpStatusCode.Forbidden => "NABDA_INSTANCE_SUSPENDED",
            _ => "NABDA_WHATSAPP_OTP_FAILED"
        };

    private static string ResolveFailureMessage(HttpStatusCode statusCode) =>
        statusCode switch
        {
            HttpStatusCode.Unauthorized => "Nabda WhatsApp OTP API key is invalid.",
            HttpStatusCode.Forbidden => "Nabda WhatsApp OTP instance is suspended.",
            _ => "Nabda WhatsApp OTP delivery failed."
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

    private sealed record NabdaSendMessageRequest(
        [property: JsonPropertyName("phone")] string Phone,
        [property: JsonPropertyName("message")] string Message);
}
