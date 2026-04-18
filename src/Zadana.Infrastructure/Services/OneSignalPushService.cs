using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Social.Support;
using Zadana.Infrastructure.Settings;

namespace Zadana.Infrastructure.Services;

public sealed class OneSignalPushService : IOneSignalPushService
{
    private readonly HttpClient _httpClient;
    private readonly OneSignalSettings _settings;
    private readonly ILogger<OneSignalPushService> _logger;

    public OneSignalPushService(
        HttpClient httpClient,
        IOptions<OneSignalSettings> settings,
        ILogger<OneSignalPushService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<OneSignalPushDispatchResult> SendToExternalUserAsync(
        string externalUserId,
        string titleAr,
        string titleEn,
        string bodyAr,
        string bodyEn,
        string? type = null,
        Guid? referenceId = null,
        string? data = null,
        string? targetUrl = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(externalUserId))
        {
            return new OneSignalPushDispatchResult(
                Attempted: false,
                Sent: false,
                Skipped: true,
                ProviderStatusCode: null,
                ProviderNotificationId: null,
                Reason: "External user id is required.");
        }

        if (!_settings.Enabled)
        {
            return new OneSignalPushDispatchResult(
                Attempted: false,
                Sent: false,
                Skipped: true,
                ProviderStatusCode: null,
                ProviderNotificationId: null,
                Reason: "OneSignal is disabled in configuration.");
        }

        if (string.IsNullOrWhiteSpace(_settings.AppId) || string.IsNullOrWhiteSpace(_settings.RestApiKey))
        {
            return new OneSignalPushDispatchResult(
                Attempted: false,
                Sent: false,
                Skipped: true,
                ProviderStatusCode: null,
                ProviderNotificationId: null,
                Reason: "OneSignal AppId or RestApiKey is not configured.");
        }

        var sanitized = NotificationPayloadHelper.Sanitize(titleAr, titleEn, bodyAr, bodyEn, type, data);
        var payload = BuildPayload(externalUserId, sanitized, referenceId, ResolveTargetUrl(targetUrl));

        using var request = new HttpRequestMessage(HttpMethod.Post, "notifications")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Key", _settings.RestApiKey);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var statusCode = (int)response.StatusCode;

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "OneSignal push send failed for external user {ExternalUserId}. Status {StatusCode}. Response: {ResponseBody}",
                    externalUserId,
                    statusCode,
                    responseBody);

                return new OneSignalPushDispatchResult(
                    Attempted: true,
                    Sent: false,
                    Skipped: false,
                    ProviderStatusCode: statusCode,
                    ProviderNotificationId: ExtractNotificationId(responseBody),
                    Reason: string.IsNullOrWhiteSpace(responseBody)
                        ? "OneSignal rejected the notification request."
                        : responseBody);
            }

            var notificationId = ExtractNotificationId(responseBody);

            _logger.LogInformation(
                "OneSignal push sent successfully for external user {ExternalUserId}. NotificationId: {NotificationId}",
                externalUserId,
                notificationId);

            return new OneSignalPushDispatchResult(
                Attempted: true,
                Sent: true,
                Skipped: false,
                ProviderStatusCode: statusCode,
                ProviderNotificationId: notificationId,
                Reason: null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OneSignal push send threw an exception for external user {ExternalUserId}", externalUserId);

            return new OneSignalPushDispatchResult(
                Attempted: true,
                Sent: false,
                Skipped: false,
                ProviderStatusCode: null,
                ProviderNotificationId: null,
                Reason: ex.Message);
        }
    }

    private Dictionary<string, object?> BuildPayload(
        string externalUserId,
        SanitizedNotificationPayload sanitized,
        Guid? referenceId,
        string? targetUrl)
    {
        var payload = new Dictionary<string, object?>
        {
            ["app_id"] = _settings.AppId,
            ["target_channel"] = "push",
            ["include_aliases"] = new Dictionary<string, string[]>
            {
                ["external_id"] = new[] { externalUserId }
            },
            ["headings"] = BuildLocalizedContent(sanitized.TitleAr, sanitized.TitleEn, "Vendor notification"),
            ["contents"] = BuildLocalizedContent(sanitized.BodyAr, sanitized.BodyEn, "You have a new vendor notification."),
            ["data"] = BuildAdditionalData(sanitized, referenceId)
        };

        if (!string.IsNullOrWhiteSpace(targetUrl))
        {
            payload["web_url"] = targetUrl;
        }

        return payload;
    }

    private static Dictionary<string, string> BuildLocalizedContent(string? arabic, string? english, string fallback)
    {
        var content = new Dictionary<string, string>();

        if (!string.IsNullOrWhiteSpace(english))
        {
            content["en"] = english;
        }

        if (!string.IsNullOrWhiteSpace(arabic))
        {
            content["ar"] = arabic;
        }

        if (content.Count == 0)
        {
            content["en"] = fallback;
        }

        return content;
    }

    private static Dictionary<string, object?> BuildAdditionalData(SanitizedNotificationPayload sanitized, Guid? referenceId)
    {
        var data = new Dictionary<string, object?>();

        if (!string.IsNullOrWhiteSpace(sanitized.Type))
        {
            data["type"] = sanitized.Type;
        }

        if (referenceId.HasValue)
        {
            data["referenceId"] = referenceId.Value;
        }

        if (!string.IsNullOrWhiteSpace(sanitized.Data))
        {
            data["payload"] = DeserializeJsonValue(sanitized.Data);
        }

        return data;
    }

    private string? ResolveTargetUrl(string? requestedTargetUrl)
    {
        if (string.IsNullOrWhiteSpace(requestedTargetUrl))
        {
            return string.IsNullOrWhiteSpace(_settings.DefaultWebUrl) ? null : _settings.DefaultWebUrl;
        }

        if (Uri.TryCreate(requestedTargetUrl, UriKind.Absolute, out var absolute))
        {
            return absolute.ToString();
        }

        if (string.IsNullOrWhiteSpace(_settings.DefaultWebUrl) ||
            !Uri.TryCreate(_settings.DefaultWebUrl, UriKind.Absolute, out var baseUri))
        {
            return requestedTargetUrl;
        }

        return new Uri(baseUri, requestedTargetUrl).ToString();
    }

    private static object? DeserializeJsonValue(string rawData)
    {
        try
        {
            return JsonSerializer.Deserialize<object>(rawData);
        }
        catch
        {
            return rawData;
        }
    }

    private static string? ExtractNotificationId(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        try
        {
            using var json = JsonDocument.Parse(responseBody);
            if (json.RootElement.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.String)
            {
                return idElement.GetString();
            }
        }
        catch
        {
            // Ignore malformed or non-JSON provider responses and keep the raw response as the reason only.
        }

        return null;
    }
}
