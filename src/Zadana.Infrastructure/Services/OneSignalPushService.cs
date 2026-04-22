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
    private const int MaxExternalIdsPerRequest = 20_000;
    private const string DefaultMobileClickAction = "FLUTTER_NOTIFICATION_CLICK";
    private const string DefaultMobileAccentColor = "FF127C8C";

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

    public Task<OneSignalPushDispatchResult> SendToExternalUserAsync(
        string externalUserId,
        string titleAr,
        string titleEn,
        string bodyAr,
        string bodyEn,
        string? type = null,
        Guid? referenceId = null,
        string? data = null,
        string? targetUrl = null,
        CancellationToken cancellationToken = default) =>
        SendToExternalUserAsync(
            externalUserId,
            titleAr,
            titleEn,
            bodyAr,
            bodyEn,
            type,
            referenceId,
            data,
            targetUrl,
            OneSignalPushProfile.Default,
            cancellationToken);

    public async Task<OneSignalPushDispatchResult> SendToExternalUserAsync(
        string externalUserId,
        string titleAr,
        string titleEn,
        string bodyAr,
        string bodyEn,
        string? type,
        Guid? referenceId,
        string? data,
        string? targetUrl,
        OneSignalPushProfile profile,
        CancellationToken cancellationToken = default)
    {
        var results = await SendToExternalUsersAsync(
            [externalUserId],
            titleAr,
            titleEn,
            bodyAr,
            bodyEn,
            type,
            referenceId,
            data,
            targetUrl,
            profile,
            cancellationToken);

        return results[0];
    }

    public async Task<IReadOnlyList<OneSignalPushDispatchResult>> SendToExternalUsersAsync(
        IReadOnlyCollection<string> externalUserIds,
        string titleAr,
        string titleEn,
        string bodyAr,
        string bodyEn,
        string? type = null,
        Guid? referenceId = null,
        string? data = null,
        string? targetUrl = null,
        OneSignalPushProfile profile = OneSignalPushProfile.Default,
        CancellationToken cancellationToken = default)
    {
        var normalizedExternalUserIds = externalUserIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (normalizedExternalUserIds.Length == 0)
        {
            return [CreateSkippedResult("At least one external user id is required.", normalizedExternalUserIds.Length)];
        }

        if (!_settings.Enabled)
        {
            return [CreateSkippedResult("OneSignal is disabled in configuration.", normalizedExternalUserIds.Length)];
        }

        if (string.IsNullOrWhiteSpace(_settings.AppId) || string.IsNullOrWhiteSpace(_settings.RestApiKey))
        {
            return [CreateSkippedResult("OneSignal AppId or RestApiKey is not configured.", normalizedExternalUserIds.Length)];
        }

        var sanitized = NotificationPayloadHelper.Sanitize(titleAr, titleEn, bodyAr, bodyEn, type, data);
        var resolvedTargetUrl = ShouldIncludeWebUrl(profile) ? ResolveTargetUrl(targetUrl) : null;
        var notificationEventId = Guid.NewGuid();

        var results = new List<OneSignalPushDispatchResult>();

        foreach (var batch in normalizedExternalUserIds.Chunk(MaxExternalIdsPerRequest))
        {
            var payload = BuildPayload(
                batch,
                sanitized,
                referenceId,
                resolvedTargetUrl,
                profile,
                notificationEventId,
                Guid.NewGuid());

            try
            {
                var payloadJson = System.Text.Json.JsonSerializer.Serialize(payload);
                _logger.LogWarning(
                    "[PUSH-DIAG] OneSignal raw payload for {ExternalUserCount} users (type={Type}, profile={Profile}): {PayloadJson}",
                    batch.Length,
                    sanitized.Type,
                    profile,
                    payloadJson);
            }
            catch
            {
                // Diagnostic logging should never break the push flow.
            }

            var result = await SendPayloadAsync(batch, payload, cancellationToken);
            results.Add(result);
        }

        return results;
    }

    private async Task<OneSignalPushDispatchResult> SendPayloadAsync(
        IReadOnlyCollection<string> externalUserIds,
        Dictionary<string, object?> payload,
        CancellationToken cancellationToken)
    {
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
                    "OneSignal push send failed for {ExternalUserCount} external users. Status {StatusCode}. Response: {ResponseBody}",
                    externalUserIds.Count,
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
                "OneSignal push sent successfully for {ExternalUserCount} external users. NotificationId: {NotificationId}",
                externalUserIds.Count,
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
            _logger.LogError(
                ex,
                "OneSignal push send threw an exception for {ExternalUserCount} external users",
                externalUserIds.Count);

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
        IReadOnlyCollection<string> externalUserIds,
        SanitizedNotificationPayload sanitized,
        Guid? referenceId,
        string? targetUrl,
        OneSignalPushProfile profile,
        Guid notificationEventId,
        Guid requestIdempotencyKey)
    {
        var payload = new Dictionary<string, object?>
        {
            ["app_id"] = _settings.AppId,
            ["idempotency_key"] = requestIdempotencyKey,
            ["collapse_id"] = notificationEventId.ToString(),
            ["target_channel"] = "push",
            ["include_aliases"] = new Dictionary<string, string[]>
            {
                ["external_id"] = externalUserIds.ToArray()
            },
            ["headings"] = BuildLocalizedContent(sanitized.TitleAr, sanitized.TitleEn, "Vendor notification"),
            ["contents"] = BuildLocalizedContent(sanitized.BodyAr, sanitized.BodyEn, "You have a new vendor notification."),
            ["data"] = BuildAdditionalData(sanitized, referenceId, notificationEventId)
        };

        if (!string.IsNullOrWhiteSpace(targetUrl))
        {
            payload["web_url"] = targetUrl;
        }

        ApplyProfile(payload, profile);

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

    private void ApplyProfile(Dictionary<string, object?> payload, OneSignalPushProfile profile)
    {
        switch (profile)
        {
            case OneSignalPushProfile.Default:
                return;

            case OneSignalPushProfile.MobileHeadsUp:
                ApplyMobileProfile(
                    payload,
                    _settings.MobileHeadsUpExistingAndroidChannelId,
                    _settings.MobileHeadsUpAndroidChannelId,
                    _settings.MobileHeadsUpPriority);
                return;

            case OneSignalPushProfile.MobileOrderUpdates:
                // Temporary operational alignment:
                // order-status pushes should behave exactly like the working test pushes
                // until the dedicated order-updates channel is verified on devices in
                // killed/terminated state.
                ApplyMobileProfile(
                    payload,
                    _settings.MobileHeadsUpExistingAndroidChannelId,
                    _settings.MobileHeadsUpAndroidChannelId,
                    _settings.MobileHeadsUpPriority);
                return;

            default:
                throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unsupported OneSignal push profile.");
        }
    }

    private static void ApplyMobileProfile(
        Dictionary<string, object?> payload,
        string? existingAndroidChannelId,
        string? androidChannelId,
        int priority)
    {
        // The Zadana mobile apps define their Android channels programmatically, so
        // OneSignal expects existing_android_channel_id instead of android_channel_id.
        if (!string.IsNullOrWhiteSpace(existingAndroidChannelId))
        {
            payload["existing_android_channel_id"] = existingAndroidChannelId;
        }
        else if (!string.IsNullOrWhiteSpace(androidChannelId))
        {
            payload["android_channel_id"] = androidChannelId;
        }

        payload["priority"] = priority;
        payload["android_accent_color"] = DefaultMobileAccentColor;
        payload["content_available"] = true;
        payload["mutable_content"] = true;
        payload["isAndroid"] = true;
        payload["isIos"] = true;
        payload["isAnyWeb"] = false;

        if (payload.TryGetValue("data", out var dataValue) &&
            dataValue is Dictionary<string, object?> data &&
            !data.ContainsKey("click_action"))
        {
            data["click_action"] = DefaultMobileClickAction;
        }
    }

    private static Dictionary<string, object?> BuildAdditionalData(
        SanitizedNotificationPayload sanitized,
        Guid? referenceId,
        Guid notificationEventId)
    {
        var data = new Dictionary<string, object?>
        {
            ["notificationId"] = notificationEventId
        };

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
            var payload = DeserializeJsonValue(sanitized.Data);
            data["payload"] = payload;
            TryMergePayloadObject(data, sanitized.Data);
        }

        return data;
    }

    private static void TryMergePayloadObject(Dictionary<string, object?> data, string rawData)
    {
        try
        {
            using var document = JsonDocument.Parse(rawData);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (data.ContainsKey(property.Name))
                {
                    continue;
                }

                data[property.Name] = DeserializeJsonValue(property.Value.GetRawText());
            }
        }
        catch
        {
            // Keep the original nested payload only if the raw data is not valid JSON object content.
        }
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

    private static bool ShouldIncludeWebUrl(OneSignalPushProfile profile) =>
        profile == OneSignalPushProfile.Default;

    private static string? FirstNonEmpty(string? primary, string? fallback) =>
        !string.IsNullOrWhiteSpace(primary) ? primary : fallback;

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

    private OneSignalPushDispatchResult CreateSkippedResult(string reason, int externalUserCount)
    {
        _logger.LogWarning(
            "OneSignal push skipped for {ExternalUserCount} external users. Reason: {Reason}",
            externalUserCount,
            reason);

        return new(
            Attempted: false,
            Sent: false,
            Skipped: true,
            ProviderStatusCode: null,
            ProviderNotificationId: null,
            Reason: reason);
    }
}
