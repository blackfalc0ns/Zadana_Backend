using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zadana.Application.Common.Interfaces;
using Zadana.Infrastructure.Settings;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Infrastructure.Services;

public sealed class WhatsAppCloudOtpService : IOtpService
{
    private readonly HttpClient _httpClient;
    private readonly WhatsAppCloudOtpSettings _settings;
    private readonly ILogger<WhatsAppCloudOtpService> _logger;

    public WhatsAppCloudOtpService(
        HttpClient httpClient,
        IOptions<WhatsAppCloudOtpSettings> settings,
        ILogger<WhatsAppCloudOtpService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendOtpSmsAsync(string phoneNumber, string otpCode, CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            _logger.LogWarning("WhatsApp Cloud OTP is disabled. OTP delivery was skipped for {Phone}.", MaskPhone(phoneNumber));
            return;
        }

        EnsureConfigured();

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

        var recipient = ToCloudRecipient(formattedPhone);
        using var request = new HttpRequestMessage(HttpMethod.Post, ResolveMessagesPath())
        {
            Content = JsonContent.Create(BuildTemplatePayload(recipient, otpCode))
        };
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_settings.AccessToken.Trim()}");

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("WhatsApp OTP template sent successfully via Cloud API to {Phone}.", MaskPhone(formattedPhone));
                return;
            }

            _logger.LogWarning(
                "WhatsApp Cloud OTP delivery failed for {Phone}. StatusCode={StatusCode}",
                MaskPhone(formattedPhone),
                (int)response.StatusCode);

            throw new ExternalServiceException(
                ResolveErrorCode(response.StatusCode),
                ResolveFailureMessage(response.StatusCode));
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ExternalServiceException(
                "WHATSAPP_CLOUD_OTP_TIMEOUT",
                "WhatsApp Cloud OTP delivery timed out.",
                ex);
        }
        catch (HttpRequestException ex)
        {
            throw new ExternalServiceException(
                "WHATSAPP_CLOUD_OTP_UNAVAILABLE",
                "WhatsApp Cloud OTP service is unavailable.",
                ex);
        }
    }

    public Task SendOtpEmailAsync(
        string emailAddress,
        string otpCode,
        CancellationToken cancellationToken = default,
        int validityMinutes = 5)
    {
        _logger.LogDebug("WhatsApp Cloud OTP service does not send email OTP.");
        return Task.CompletedTask;
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_settings.AccessToken))
        {
            throw new ExternalServiceException(
                "WHATSAPP_CLOUD_OTP_NOT_CONFIGURED",
                "WhatsApp Cloud OTP is enabled but no access token is configured.");
        }

        if (string.IsNullOrWhiteSpace(_settings.PhoneNumberId))
        {
            throw new ExternalServiceException(
                "WHATSAPP_CLOUD_OTP_NOT_CONFIGURED",
                "WhatsApp Cloud OTP is enabled but no phone number ID is configured.");
        }

        if (string.IsNullOrWhiteSpace(_settings.TemplateName))
        {
            throw new ExternalServiceException(
                "WHATSAPP_CLOUD_OTP_NOT_CONFIGURED",
                "WhatsApp Cloud OTP is enabled but no template name is configured.");
        }
    }

    private WhatsAppTemplateMessageRequest BuildTemplatePayload(string recipient, string otpCode) =>
        new(
            MessagingProduct: "whatsapp",
            RecipientType: "individual",
            To: recipient,
            Type: "template",
            Template: new WhatsAppTemplate(
                Name: _settings.TemplateName.Trim(),
                Language: new WhatsAppTemplateLanguage(_settings.LanguageCode.Trim()),
                Components:
                [
                    new WhatsAppTemplateComponent(
                        Type: "body",
                        SubType: null,
                        Index: null,
                        Parameters:
                        [
                            new WhatsAppTemplateParameter(Type: "text", Text: otpCode, CouponCode: null)
                        ]),
                    new WhatsAppTemplateComponent(
                        Type: "button",
                        SubType: "copy_code",
                        Index: _settings.CopyCodeButtonIndex.ToString(),
                        Parameters:
                        [
                            new WhatsAppTemplateParameter(Type: "coupon_code", Text: null, CouponCode: otpCode)
                        ])
                ]));

    private string ResolveMessagesPath()
    {
        var graphVersion = string.IsNullOrWhiteSpace(_settings.GraphVersion)
            ? "v23.0"
            : _settings.GraphVersion.Trim().Trim('/');

        return $"/{graphVersion}/{Uri.EscapeDataString(_settings.PhoneNumberId.Trim())}/messages";
    }

    private static string ToCloudRecipient(string formattedPhone) =>
        new(formattedPhone.Where(char.IsDigit).ToArray());

    private static string ResolveErrorCode(HttpStatusCode statusCode) =>
        statusCode switch
        {
            HttpStatusCode.Unauthorized => "WHATSAPP_CLOUD_INVALID_ACCESS_TOKEN",
            HttpStatusCode.Forbidden => "WHATSAPP_CLOUD_FORBIDDEN",
            HttpStatusCode.TooManyRequests => "WHATSAPP_CLOUD_RATE_LIMITED",
            _ => "WHATSAPP_CLOUD_OTP_FAILED"
        };

    private static string ResolveFailureMessage(HttpStatusCode statusCode) =>
        statusCode switch
        {
            HttpStatusCode.Unauthorized => "WhatsApp Cloud API access token is invalid.",
            HttpStatusCode.Forbidden => "WhatsApp Cloud API rejected the OTP request.",
            HttpStatusCode.TooManyRequests => "WhatsApp Cloud API rate limit has been exceeded.",
            _ => "WhatsApp Cloud OTP delivery failed."
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

    private sealed record WhatsAppTemplateMessageRequest(
        [property: JsonPropertyName("messaging_product")] string MessagingProduct,
        [property: JsonPropertyName("recipient_type")] string RecipientType,
        [property: JsonPropertyName("to")] string To,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("template")] WhatsAppTemplate Template);

    private sealed record WhatsAppTemplate(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("language")] WhatsAppTemplateLanguage Language,
        [property: JsonPropertyName("components")] IReadOnlyList<WhatsAppTemplateComponent> Components);

    private sealed record WhatsAppTemplateLanguage(
        [property: JsonPropertyName("code")] string Code);

    private sealed record WhatsAppTemplateComponent(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("sub_type")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? SubType,
        [property: JsonPropertyName("index")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? Index,
        [property: JsonPropertyName("parameters")] IReadOnlyList<WhatsAppTemplateParameter> Parameters);

    private sealed record WhatsAppTemplateParameter(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("text")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? Text,
        [property: JsonPropertyName("coupon_code")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? CouponCode);
}
