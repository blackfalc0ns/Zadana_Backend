using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Social.Support;
using Zadana.Infrastructure.Persistence;
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
    private readonly IServiceScopeFactory _scopeFactory;
    public OneSignalPushService(
        HttpClient httpClient,
        IOptions<OneSignalSettings> settings,
        ILogger<OneSignalPushService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
        _scopeFactory = scopeFactory;

        ValidateConfigurationAtStartup();
    }

    private void ValidateConfigurationAtStartup()
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("[PUSH-CONFIG] OneSignal is disabled in configuration. Push notifications will be skipped.");
            return;
        }

        var customerAppId = ResolveSettingValue("OneSignal__AppId", _settings.AppId);
        var customerRestApiKey = ResolveSettingValue("OneSignal__RestApiKey", _settings.RestApiKey);

        if (string.IsNullOrWhiteSpace(customerAppId) || string.IsNullOrWhiteSpace(customerRestApiKey))
        {
            _logger.LogError(
                "[PUSH-CONFIG] OneSignal is enabled but Customer credentials are incomplete. " +
                "OneSignal__AppId set: {HasAppId}. OneSignal__RestApiKey set: {HasRestApiKey}. " +
                "All push notifications will fail.",
                !string.IsNullOrWhiteSpace(customerAppId),
                !string.IsNullOrWhiteSpace(customerRestApiKey));
        }

        WarnIfSeparateAppCredentialsInconsistent(
            applicationName: "Driver",
            appIdEnvVar: "OneSignal__DriverAppId",
            appIdConfigured: _settings.DriverAppId,
            restApiKeyEnvVar: "OneSignal__DriverRestApiKey",
            restApiKeyConfigured: _settings.DriverRestApiKey);

        WarnIfSeparateAppCredentialsInconsistent(
            applicationName: "AdminWeb",
            appIdEnvVar: "OneSignal__AdminWebAppId",
            appIdConfigured: _settings.AdminWebAppId,
            restApiKeyEnvVar: "OneSignal__AdminWebRestApiKey",
            restApiKeyConfigured: _settings.AdminWebRestApiKey);
    }

    private void WarnIfSeparateAppCredentialsInconsistent(
        string applicationName,
        string appIdEnvVar,
        string appIdConfigured,
        string restApiKeyEnvVar,
        string restApiKeyConfigured)
    {
        var hasAppId = !string.IsNullOrWhiteSpace(ResolveSettingValue(appIdEnvVar, appIdConfigured));
        var hasRestApiKey = !string.IsNullOrWhiteSpace(ResolveSettingValue(restApiKeyEnvVar, restApiKeyConfigured));

        if (hasAppId == hasRestApiKey)
        {
            return;
        }

        _logger.LogError(
            "[PUSH-CONFIG] {ApplicationName} OneSignal credentials are inconsistent at startup. " +
            "{AppIdEnvVar} set: {HasAppId}. {RestApiKeyEnvVar} set: {HasRestApiKey}. " +
            "Both must be configured together (or both left empty to fall back to the Customer app). " +
            "Until this is fixed, all dedicated push notifications for this app will be skipped to avoid 401 Access denied from OneSignal.",
            applicationName,
            appIdEnvVar,
            hasAppId,
            restApiKeyEnvVar,
            hasRestApiKey);
    }

    public async Task<OneSignalPushDispatchResult> SendMobileNotificationAsync(
        OneSignalMobilePushRequest request,
        CancellationToken cancellationToken = default)
    {
        var results = await SendToExternalUsersCoreAsync(
            [request.ExternalUserId],
            request.TitleAr,
            request.TitleEn,
            request.BodyAr,
            request.BodyEn,
            request.Type,
            request.ReferenceId,
            request.Data,
            request.TargetUrl,
            request.Profile,
            request.Category,
            requireRegisteredDevices: true,
            cancellationToken,
            request.TargetApplication);

        var result = results[0];

        // Fallback: if skipped because no registered devices matched, retry without the device requirement.
        // This ensures push delivery even when the driver app hasn't registered a device record in the DB.
        // Important: only retry when the skip reason is "no devices". Do NOT retry when the skip reason
        // is a configuration error (missing AppId/RestApiKey) — that would just produce the same failure
        // and pollute logs without any chance of success.
        if (result.Skipped && !result.Sent && IsRetryableSkipReason(result.Reason))
        {
            _logger.LogWarning(
                "[PUSH-FALLBACK] No registered device found for {ExternalUserId} (category={Category}). Retrying without device requirement.",
                request.ExternalUserId,
                request.Category);

            var fallbackResults = await SendToExternalUsersCoreAsync(
                [request.ExternalUserId],
                request.TitleAr,
                request.TitleEn,
                request.BodyAr,
                request.BodyEn,
                request.Type,
                request.ReferenceId,
                request.Data,
                request.TargetUrl,
                request.Profile,
                request.Category,
                requireRegisteredDevices: false,
                cancellationToken,
                request.TargetApplication);

            return fallbackResults[0];
        }

        return result;
    }

    private static bool IsRetryableSkipReason(string? reason) =>
        !string.IsNullOrWhiteSpace(reason) &&
        reason.Contains("No registered push devices found", StringComparison.OrdinalIgnoreCase);

    public async Task<OneSignalPushDispatchResult> SendMobileNotificationDirectAsync(
        OneSignalMobilePushRequest request,
        CancellationToken cancellationToken = default)
    {
        var results = await SendToExternalUsersCoreAsync(
            [request.ExternalUserId],
            request.TitleAr,
            request.TitleEn,
            request.BodyAr,
            request.BodyEn,
            request.Type,
            request.ReferenceId,
            request.Data,
            request.TargetUrl,
            request.Profile,
            request.Category,
            requireRegisteredDevices: false,
            cancellationToken,
            request.TargetApplication);

        return results[0];
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
        var results = await SendToExternalUsersCoreAsync(
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
            category: null,
            requireRegisteredDevices: false,
            cancellationToken);

        return results[0];
    }

    public Task<IReadOnlyList<OneSignalPushDispatchResult>> SendToExternalUsersAsync(
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
        CancellationToken cancellationToken = default) =>
        SendToExternalUsersCoreAsync(
            externalUserIds,
            titleAr,
            titleEn,
            bodyAr,
            bodyEn,
            type,
            referenceId,
            data,
            targetUrl,
            profile,
            category: null,
            requireRegisteredDevices: false,
            cancellationToken);

    public Task<IReadOnlyList<OneSignalPushDispatchResult>> SendToExternalUsersAsync(
        IReadOnlyCollection<string> externalUserIds,
        string titleAr,
        string titleEn,
        string bodyAr,
        string bodyEn,
        string? type,
        Guid? referenceId,
        string? data,
        string? targetUrl,
        OneSignalPushProfile profile,
        OneSignalApplicationTarget targetApplication,
        CancellationToken cancellationToken = default) =>
        SendToExternalUsersCoreAsync(
            externalUserIds,
            titleAr,
            titleEn,
            bodyAr,
            bodyEn,
            type,
            referenceId,
            data,
            targetUrl,
            profile,
            category: null,
            requireRegisteredDevices: false,
            cancellationToken,
            targetApplication);

    private async Task<IReadOnlyList<OneSignalPushDispatchResult>> SendToExternalUsersCoreAsync(
        IReadOnlyCollection<string> externalUserIds,
        string titleAr,
        string titleEn,
        string bodyAr,
        string bodyEn,
        string? type,
        Guid? referenceId,
        string? data,
        string? targetUrl,
        OneSignalPushProfile profile,
        string? category,
        bool requireRegisteredDevices,
        CancellationToken cancellationToken,
        OneSignalApplicationTarget? targetApplication = null)
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

        var appConfiguration = targetApplication.HasValue
            ? ResolveAppConfiguration(targetApplication.Value)
            : ResolveAppConfiguration(profile, category);
        var resolvedTargetApplication = targetApplication ?? ResolveTargetApplication(category);

        if (string.IsNullOrWhiteSpace(appConfiguration.AppId) || string.IsNullOrWhiteSpace(appConfiguration.RestApiKey))
        {
            return [CreateSkippedResult(BuildMissingConfigurationReason(resolvedTargetApplication), normalizedExternalUserIds.Length)];
        }

        var sanitized = NotificationPayloadHelper.Sanitize(titleAr, titleEn, bodyAr, bodyEn, type, data);
        var resolvedTargetUrl = ShouldIncludeWebUrl(profile) ? ResolveTargetUrl(targetUrl, targetApplication) : null;
        var notificationEventId = Guid.NewGuid();

        var recipientIdentity = await ResolvePushRecipientIdentityAsync(
            normalizedExternalUserIds,
            resolvedTargetApplication,
            cancellationToken);

        var recipientsByLocale = await ResolveRecipientsByLocaleAsync(
            recipientIdentity.LookupExternalUserIds,
            category,
            requireRegisteredDevices,
            cancellationToken);

        if (recipientsByLocale.Count == 0)
        {
            var skipReason = await ResolveNoRecipientsSkipReasonAsync(
                recipientIdentity.LookupExternalUserIds,
                cancellationToken);

            return [CreateSkippedResult(skipReason, normalizedExternalUserIds.Length)];
        }

        var results = new List<OneSignalPushDispatchResult>();

        foreach (var localeBatch in recipientsByLocale)
        {
            var preferredLocale = ResolvePreferredLocaleForBatch(localeBatch.Locale, resolvedTargetApplication);
            var localePushExternalUserIds = localeBatch.ExternalUserIds
                .Select(recipientIdentity.ResolvePushExternalUserId)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            foreach (var batch in localePushExternalUserIds.Chunk(MaxExternalIdsPerRequest))
            {
                var pushBatch = batch.ToHashSet(StringComparer.Ordinal);
                var lookupBatch = localeBatch.ExternalUserIds
                    .Where(id => pushBatch.Contains(recipientIdentity.ResolvePushExternalUserId(id)))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

                if (resolvedTargetApplication is OneSignalApplicationTarget.AdminWeb
                    or OneSignalApplicationTarget.Driver
                    or OneSignalApplicationTarget.Customer)
                {
                    var subscriptionFirstPayload = await BuildSubscriptionPayloadAsync(
                        lookupBatch,
                        sanitized,
                        referenceId,
                        resolvedTargetUrl,
                        appConfiguration.AppId,
                        appConfiguration.RestApiKey,
                        profile,
                        notificationEventId,
                        Guid.NewGuid(),
                        preferredLocale,
                        category,
                        cancellationToken);

                    if (subscriptionFirstPayload is not null)
                    {
                        var subscriptionFirstResult = await SendPayloadAsync(subscriptionFirstPayload, cancellationToken);
                        if (subscriptionFirstResult.Sent)
                        {
                            results.Add(subscriptionFirstResult);
                            continue;
                        }
                    }
                }

                var preparedPayload = BuildPayload(
                    batch,
                    sanitized,
                    referenceId,
                    resolvedTargetUrl,
                    appConfiguration.AppId,
                    appConfiguration.RestApiKey,
                    profile,
                    notificationEventId,
                    Guid.NewGuid(),
                    preferredLocale);

                try
                {
                    var payloadJson = System.Text.Json.JsonSerializer.Serialize(preparedPayload.Payload);
                    _logger.LogWarning(
                        "[PUSH-DIAG] OneSignal prepared payload. AppId: {AppId}. RestApiKeyLength: {RestApiKeyLength}. RestApiKeySuffix: {RestApiKeySuffix}. ExternalUserCount: {ExternalUserCount}. ExternalIdBatch: {ExternalIdBatch}. Profile: {Profile}. Type: {Type}. ReferenceId: {ReferenceId}. NotificationEventId: {NotificationEventId}. PreferredLocale: {PreferredLocale}. Channel: {Channel}. DataKeys: {DataKeys}. PayloadJson: {PayloadJson}",
                        preparedPayload.AppId,
                        preparedPayload.RestApiKey?.Length ?? 0,
                        MaskSecretSuffix(preparedPayload.RestApiKey),
                        preparedPayload.ExternalUserCount,
                        preparedPayload.ExternalIdBatch,
                        preparedPayload.Profile,
                        preparedPayload.Type,
                        preparedPayload.ReferenceId,
                        preparedPayload.NotificationEventId,
                        preparedPayload.PreferredLocale,
                        preparedPayload.Channel,
                        preparedPayload.DataKeys,
                        payloadJson);
                }
                catch
                {
                    // Diagnostic logging should never break the push flow.
                }

                var result = await SendPayloadAsync(preparedPayload, cancellationToken);
                if (!result.Sent && HasProviderRecipientErrors(result.Reason))
                {
                    var subscriptionPayload = await BuildSubscriptionPayloadAsync(
                        lookupBatch,
                        sanitized,
                        referenceId,
                        resolvedTargetUrl,
                        appConfiguration.AppId,
                        appConfiguration.RestApiKey,
                        profile,
                        notificationEventId,
                        Guid.NewGuid(),
                        preferredLocale,
                        category,
                        cancellationToken);

                    subscriptionPayload ??= await BuildProviderSubscriptionPayloadAsync(
                        batch.Concat(lookupBatch).Distinct(StringComparer.Ordinal).ToArray(),
                        sanitized,
                        referenceId,
                        resolvedTargetUrl,
                        appConfiguration.AppId,
                        appConfiguration.RestApiKey,
                        profile,
                        notificationEventId,
                        Guid.NewGuid(),
                        preferredLocale,
                        cancellationToken);

                    if (subscriptionPayload is not null)
                    {
                        _logger.LogWarning(
                            "[PUSH-FALLBACK] Retrying OneSignal push using registered subscription ids for ExternalIdBatch: {ExternalIdBatch}. SubscriptionCount: {SubscriptionCount}. Type: {Type}. ReferenceId: {ReferenceId}",
                            preparedPayload.ExternalIdBatch,
                            subscriptionPayload.ExternalUserCount,
                            preparedPayload.Type,
                            preparedPayload.ReferenceId);

                        result = await SendPayloadAsync(subscriptionPayload, cancellationToken);
                    }
                }
                results.Add(result);
            }
        }

        return results;
    }

    private async Task<PushRecipientIdentity> ResolvePushRecipientIdentityAsync(
        IReadOnlyCollection<string> externalUserIds,
        OneSignalApplicationTarget targetApplication,
        CancellationToken cancellationToken)
    {
        if (targetApplication != OneSignalApplicationTarget.Driver)
        {
            return PushRecipientIdentity.PassThrough(externalUserIds);
        }

        var parsedIds = externalUserIds
            .Select(id => Guid.TryParse(id, out var parsedId)
                ? new ParsedExternalUserId(id, parsedId)
                : new ParsedExternalUserId(id, null))
            .ToArray();

        var guidIds = parsedIds
            .Where(item => item.ParsedId.HasValue)
            .Select(item => item.ParsedId!.Value)
            .Distinct()
            .ToArray();

        if (guidIds.Length == 0)
        {
            return PushRecipientIdentity.PassThrough(externalUserIds);
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var drivers = await dbContext.Drivers
            .AsNoTracking()
            .Where(driver => guidIds.Contains(driver.UserId) || guidIds.Contains(driver.Id))
            .Select(driver => new DriverPushIdentity(driver.UserId, driver.Id))
            .ToListAsync(cancellationToken);

        var byUserId = drivers.ToDictionary(
            item => item.UserId,
            item => item,
            EqualityComparer<Guid>.Default);
        var byDriverId = drivers.ToDictionary(
            item => item.DriverId,
            item => item,
            EqualityComparer<Guid>.Default);

        var pushExternalUserIdByLookup = new Dictionary<string, string>(StringComparer.Ordinal);
        var lookupExternalUserIds = new List<string>(parsedIds.Length);

        foreach (var parsed in parsedIds)
        {
            if (parsed.ParsedId.HasValue && byUserId.TryGetValue(parsed.ParsedId.Value, out var byUserMatch))
            {
                var lookupExternalUserId = byUserMatch.UserId.ToString();
                lookupExternalUserIds.Add(lookupExternalUserId);
                // Driver mobile app registers OneSignal.login(userId), so push must target userId.
                pushExternalUserIdByLookup[lookupExternalUserId] = lookupExternalUserId;
                continue;
            }

            if (parsed.ParsedId.HasValue && byDriverId.TryGetValue(parsed.ParsedId.Value, out var byDriverMatch))
            {
                var lookupExternalUserId = byDriverMatch.UserId.ToString();
                lookupExternalUserIds.Add(lookupExternalUserId);
                pushExternalUserIdByLookup[lookupExternalUserId] = lookupExternalUserId;
                continue;
            }

            lookupExternalUserIds.Add(parsed.RawId);
            pushExternalUserIdByLookup[parsed.RawId] = parsed.RawId;
        }

        return new PushRecipientIdentity(
            lookupExternalUserIds.Distinct(StringComparer.Ordinal).ToArray(),
            pushExternalUserIdByLookup);
    }

    private async Task<OneSignalPushDispatchResult> SendPayloadAsync(
        PreparedOneSignalPayload preparedPayload,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "notifications")
        {
            Content = JsonContent.Create(preparedPayload.Payload)
        };
        request.Headers.TryAddWithoutValidation(
            "Authorization",
            $"Key {preparedPayload.RestApiKey.Trim()}");

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var statusCode = (int)response.StatusCode;
            var notificationId = ExtractNotificationId(responseBody);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "[PUSH-DIAG] OneSignal push provider response failed. ExternalUserCount: {ExternalUserCount}. ExternalIdBatch: {ExternalIdBatch}. Profile: {Profile}. Type: {Type}. ReferenceId: {ReferenceId}. NotificationEventId: {NotificationEventId}. PreferredLocale: {PreferredLocale}. Channel: {Channel}. DataKeys: {DataKeys}. StatusCode: {StatusCode}. ProviderNotificationId: {ProviderNotificationId}. ResponseBody: {ResponseBody}",
                    preparedPayload.ExternalUserCount,
                    preparedPayload.ExternalIdBatch,
                    preparedPayload.Profile,
                    preparedPayload.Type,
                    preparedPayload.ReferenceId,
                    preparedPayload.NotificationEventId,
                    preparedPayload.PreferredLocale,
                    preparedPayload.Channel,
                    preparedPayload.DataKeys,
                    statusCode,
                    notificationId,
                    responseBody);

                return new OneSignalPushDispatchResult(
                    Attempted: true,
                    Sent: false,
                    Skipped: false,
                    ProviderStatusCode: statusCode,
                    ProviderNotificationId: notificationId,
                    Reason:
                        string.IsNullOrWhiteSpace(responseBody)
                            ? "OneSignal rejected the notification request."
                            : responseBody);
            }

            if (HasProviderRecipientErrors(responseBody) && !HasSuccessfulNotificationId(responseBody))
            {
                _logger.LogWarning(
                    "[PUSH-DIAG] OneSignal push provider accepted request but reported recipient errors. ExternalUserCount: {ExternalUserCount}. ExternalIdBatch: {ExternalIdBatch}. Profile: {Profile}. Type: {Type}. ReferenceId: {ReferenceId}. NotificationEventId: {NotificationEventId}. PreferredLocale: {PreferredLocale}. Channel: {Channel}. DataKeys: {DataKeys}. StatusCode: {StatusCode}. ProviderNotificationId: {ProviderNotificationId}. ResponseBody: {ResponseBody}",
                    preparedPayload.ExternalUserCount,
                    preparedPayload.ExternalIdBatch,
                    preparedPayload.Profile,
                    preparedPayload.Type,
                    preparedPayload.ReferenceId,
                    preparedPayload.NotificationEventId,
                    preparedPayload.PreferredLocale,
                    preparedPayload.Channel,
                    preparedPayload.DataKeys,
                    statusCode,
                    notificationId,
                    responseBody);

                return new OneSignalPushDispatchResult(
                    Attempted: true,
                    Sent: false,
                    Skipped: false,
                    ProviderStatusCode: statusCode,
                    ProviderNotificationId: notificationId,
                    Reason:
                        string.IsNullOrWhiteSpace(responseBody)
                            ? "OneSignal reported recipient errors."
                            : responseBody);
            }

            if (HasProviderRecipientErrors(responseBody))
            {
                _logger.LogWarning(
                    "[PUSH-DIAG] OneSignal push delivered with partial recipient errors (stale subscriptions ignored). ExternalUserCount: {ExternalUserCount}. ExternalIdBatch: {ExternalIdBatch}. ProviderNotificationId: {ProviderNotificationId}. ResponseBody: {ResponseBody}",
                    preparedPayload.ExternalUserCount,
                    preparedPayload.ExternalIdBatch,
                    notificationId,
                    responseBody);
            }

            _logger.LogInformation(
                "[PUSH-DIAG] OneSignal push provider response succeeded. ExternalUserCount: {ExternalUserCount}. ExternalIdBatch: {ExternalIdBatch}. Profile: {Profile}. Type: {Type}. ReferenceId: {ReferenceId}. NotificationEventId: {NotificationEventId}. PreferredLocale: {PreferredLocale}. Channel: {Channel}. DataKeys: {DataKeys}. StatusCode: {StatusCode}. ProviderNotificationId: {ProviderNotificationId}. ResponseBody: {ResponseBody}",
                preparedPayload.ExternalUserCount,
                preparedPayload.ExternalIdBatch,
                preparedPayload.Profile,
                preparedPayload.Type,
                preparedPayload.ReferenceId,
                preparedPayload.NotificationEventId,
                preparedPayload.PreferredLocale,
                preparedPayload.Channel,
                preparedPayload.DataKeys,
                statusCode,
                notificationId,
                responseBody);

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
                "[PUSH-DIAG] OneSignal push send threw an exception. ExternalUserCount: {ExternalUserCount}. ExternalIdBatch: {ExternalIdBatch}. Profile: {Profile}. Type: {Type}. ReferenceId: {ReferenceId}. NotificationEventId: {NotificationEventId}. PreferredLocale: {PreferredLocale}. Channel: {Channel}. DataKeys: {DataKeys}",
                preparedPayload.ExternalUserCount,
                preparedPayload.ExternalIdBatch,
                preparedPayload.Profile,
                preparedPayload.Type,
                preparedPayload.ReferenceId,
                preparedPayload.NotificationEventId,
                preparedPayload.PreferredLocale,
                preparedPayload.Channel,
                preparedPayload.DataKeys);

            return new OneSignalPushDispatchResult(
                Attempted: true,
                Sent: false,
                Skipped: false,
                ProviderStatusCode: null,
                ProviderNotificationId: null,
                Reason: ex.Message);
        }
    }

    private PreparedOneSignalPayload BuildPayload(
        IReadOnlyCollection<string> externalUserIds,
        SanitizedNotificationPayload sanitized,
        Guid? referenceId,
        string? targetUrl,
        string appId,
        string restApiKey,
        OneSignalPushProfile profile,
        Guid notificationEventId,
        Guid requestIdempotencyKey,
        string? preferredLocale)
    {
        var payload = new Dictionary<string, object?>
        {
            ["app_id"] = appId,
            ["idempotency_key"] = requestIdempotencyKey,
            ["collapse_id"] = notificationEventId.ToString(),
            ["target_channel"] = "push",
            ["include_aliases"] = new Dictionary<string, string[]>
            {
                ["external_id"] = externalUserIds.ToArray()
            },
            ["headings"] = BuildLocalizedContent(sanitized.TitleAr, sanitized.TitleEn, "Vendor notification", preferredLocale),
            ["contents"] = BuildLocalizedContent(sanitized.BodyAr, sanitized.BodyEn, "You have a new vendor notification.", preferredLocale),
            ["data"] = BuildAdditionalData(sanitized, referenceId, notificationEventId)
        };

        if (!string.IsNullOrWhiteSpace(targetUrl))
        {
            payload["web_url"] = targetUrl;
        }

        ApplyProfile(payload, profile);

        return new PreparedOneSignalPayload(
            payload,
            appId,
            externalUserIds.Count,
            string.Join(",", externalUserIds),
            restApiKey,
            profile,
            referenceId,
            notificationEventId,
            sanitized.Type,
            preferredLocale,
            ResolveChannel(payload),
            ResolveDataKeys(payload));
    }

    private async Task<PreparedOneSignalPayload?> BuildSubscriptionPayloadAsync(
        IReadOnlyCollection<string> externalUserIds,
        SanitizedNotificationPayload sanitized,
        Guid? referenceId,
        string? targetUrl,
        string appId,
        string restApiKey,
        OneSignalPushProfile profile,
        Guid notificationEventId,
        Guid requestIdempotencyKey,
        string? preferredLocale,
        string? category,
        CancellationToken cancellationToken)
    {
        var subscriptionIds = await ResolveSubscriptionIdsAsync(externalUserIds, category, cancellationToken);
        if (subscriptionIds.Length == 0)
        {
            return null;
        }

        var payload = new Dictionary<string, object?>
        {
            ["app_id"] = appId,
            ["idempotency_key"] = requestIdempotencyKey,
            ["collapse_id"] = notificationEventId.ToString(),
            ["target_channel"] = "push",
            ["include_subscription_ids"] = subscriptionIds,
            ["headings"] = BuildLocalizedContent(sanitized.TitleAr, sanitized.TitleEn, "Vendor notification", preferredLocale),
            ["contents"] = BuildLocalizedContent(sanitized.BodyAr, sanitized.BodyEn, "You have a new vendor notification.", preferredLocale),
            ["data"] = BuildAdditionalData(sanitized, referenceId, notificationEventId)
        };

        if (!string.IsNullOrWhiteSpace(targetUrl))
        {
            payload["web_url"] = targetUrl;
        }

        ApplyProfile(payload, profile);

        return new PreparedOneSignalPayload(
            payload,
            appId,
            subscriptionIds.Length,
            string.Join(",", subscriptionIds),
            restApiKey,
            profile,
            referenceId,
            notificationEventId,
            sanitized.Type,
            preferredLocale,
            ResolveChannel(payload),
            ResolveDataKeys(payload));
    }

    private async Task<PreparedOneSignalPayload?> BuildProviderSubscriptionPayloadAsync(
        IReadOnlyCollection<string> externalUserIds,
        SanitizedNotificationPayload sanitized,
        Guid? referenceId,
        string? targetUrl,
        string appId,
        string restApiKey,
        OneSignalPushProfile profile,
        Guid notificationEventId,
        Guid requestIdempotencyKey,
        string? preferredLocale,
        CancellationToken cancellationToken)
    {
        var subscriptionIds = await ResolveProviderSubscriptionIdsAsync(
            externalUserIds,
            appId,
            restApiKey,
            cancellationToken);

        if (subscriptionIds.Length == 0)
        {
            return null;
        }

        var payload = new Dictionary<string, object?>
        {
            ["app_id"] = appId,
            ["idempotency_key"] = requestIdempotencyKey,
            ["collapse_id"] = notificationEventId.ToString(),
            ["target_channel"] = "push",
            ["include_subscription_ids"] = subscriptionIds,
            ["headings"] = BuildLocalizedContent(sanitized.TitleAr, sanitized.TitleEn, "Vendor notification", preferredLocale),
            ["contents"] = BuildLocalizedContent(sanitized.BodyAr, sanitized.BodyEn, "You have a new vendor notification.", preferredLocale),
            ["data"] = BuildAdditionalData(sanitized, referenceId, notificationEventId)
        };

        if (!string.IsNullOrWhiteSpace(targetUrl))
        {
            payload["web_url"] = targetUrl;
        }

        ApplyProfile(payload, profile);

        return new PreparedOneSignalPayload(
            payload,
            appId,
            subscriptionIds.Length,
            string.Join(",", externalUserIds),
            restApiKey,
            profile,
            referenceId,
            notificationEventId,
            sanitized.Type,
            preferredLocale,
            ResolveChannel(payload),
            ResolveDataKeys(payload));
    }

    private async Task<string[]> ResolveProviderSubscriptionIdsAsync(
        IReadOnlyCollection<string> externalUserIds,
        string appId,
        string restApiKey,
        CancellationToken cancellationToken)
    {
        var subscriptionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var externalUserId in externalUserIds.Where(id => !string.IsNullOrWhiteSpace(id)))
        {
            if (!Guid.TryParse(externalUserId.Trim(), out _))
            {
                continue;
            }

            var escapedAppId = Uri.EscapeDataString(appId);
            var escapedExternalUserId = Uri.EscapeDataString(externalUserId.Trim());
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"apps/{escapedAppId}/users/by/external_id/{escapedExternalUserId}");
            request.Headers.TryAddWithoutValidation(
                "Authorization",
                $"Key {restApiKey.Trim()}");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "[PUSH-FALLBACK] Could not resolve OneSignal user subscriptions for ExternalUserId: {ExternalUserId}. StatusCode: {StatusCode}. ResponseBody: {ResponseBody}",
                    externalUserId,
                    (int)response.StatusCode,
                    responseBody);
                continue;
            }

            try
            {
                using var json = JsonDocument.Parse(responseBody);
                if (!json.RootElement.TryGetProperty("subscriptions", out var subscriptions) ||
                    subscriptions.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var subscription in subscriptions.EnumerateArray())
                {
                    if (!subscription.TryGetProperty("enabled", out var enabled) ||
                        enabled.ValueKind != JsonValueKind.True ||
                        !subscription.TryGetProperty("id", out var idElement))
                    {
                        continue;
                    }

                    var subscriptionId = idElement.GetString();
                    if (IsValidOneSignalSubscriptionId(subscriptionId))
                    {
                        subscriptionIds.Add(subscriptionId!.Trim());
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(
                    ex,
                    "[PUSH-FALLBACK] Could not parse OneSignal user subscriptions for ExternalUserId: {ExternalUserId}.",
                    externalUserId);
            }
        }

        return subscriptionIds.ToArray();
    }

    private async Task<string[]> ResolveSubscriptionIdsAsync(
        IReadOnlyCollection<string> externalUserIds,
        string? category,
        CancellationToken cancellationToken)
    {
        var userIds = externalUserIds
            .Select(externalUserId => Guid.TryParse(externalUserId, out var userId) ? userId : Guid.Empty)
            .Where(userId => userId != Guid.Empty)
            .Distinct()
            .ToArray();

        if (userIds.Length == 0)
        {
            return [];
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var query = dbContext.UserPushDevices
            .AsNoTracking()
            .Where(device => userIds.Contains(device.UserId) && device.IsActive && device.NotificationsEnabled);

        query = ApplyCategoryFilter(query, category);

        var registeredDevices = await query
            .OrderByDescending(device => device.LastSeenAtUtc)
            .ThenByDescending(device => device.LastRegisteredAtUtc)
            .Select(device => new { device.UserId, device.DeviceToken })
            .ToArrayAsync(cancellationToken);

        // Prefer only the freshest subscription per user so stale OneSignal player ids
        // do not poison the batch and cause the provider response to be treated as a failure.
        var subscriptionIds = registeredDevices
            .GroupBy(device => device.UserId)
            .Select(group => group
                .Select(device => device.DeviceToken.Trim())
                .FirstOrDefault(IsValidOneSignalSubscriptionId))
            .Where(subscriptionId => subscriptionId is not null)
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var ignoredTokenCount = registeredDevices.Count(device => !IsValidOneSignalSubscriptionId(device.DeviceToken));
        if (ignoredTokenCount > 0)
        {
            _logger.LogWarning(
                "[PUSH-FALLBACK] Ignored {IgnoredTokenCount} registered push device tokens because they are not valid OneSignal subscription UUIDs. " +
                "The mobile app must register OneSignalSubscriptionId separately from the FCM/APNS device token.",
                ignoredTokenCount);
        }

        return subscriptionIds;
    }

    internal static bool IsValidOneSignalSubscriptionId(string? value) =>
        !string.IsNullOrWhiteSpace(value) && Guid.TryParse(value.Trim(), out _);

    private static Dictionary<string, string> BuildLocalizedContent(
        string? arabic,
        string? english,
        string fallback,
        string? preferredLocale)
    {
        var englishText = FirstNonEmpty(english, arabic, fallback);
        var arabicText = FirstNonEmpty(arabic, english, fallback);

        // OneSignal decides which language to display from each subscriber's own
        // OneSignal language tag (auto-detected by the browser) and falls back to the
        // "en" entry when there is no match. That makes the displayed language depend on
        // the browser instead of the recipient's registered device locale, so the same
        // alert can show in English on one browser and Arabic on another.
        // When we already resolved the recipient batch's locale, pin that language into
        // every slot (including the "en" fallback) so the chosen language wins deterministically.
        var normalizedLocale = NormalizeLocale(preferredLocale);
        if (normalizedLocale == "ar")
        {
            return new Dictionary<string, string>
            {
                ["en"] = arabicText,
                ["ar"] = arabicText
            };
        }

        if (normalizedLocale == "en")
        {
            return new Dictionary<string, string>
            {
                ["en"] = englishText,
                ["ar"] = englishText
            };
        }

        return new Dictionary<string, string>
        {
            ["en"] = englishText,
            ["ar"] = arabicText
        };
    }

    private async Task<IReadOnlyList<LocalizedRecipientBatch>> ResolveRecipientsByLocaleAsync(
        IReadOnlyCollection<string> externalUserIds,
        string? category,
        bool requireRegisteredDevices,
        CancellationToken cancellationToken)
    {
        var parsedUserIds = externalUserIds
            .Select(externalUserId =>
            {
                var parsed = Guid.TryParse(externalUserId, out var userId);
                return new
                {
                    ExternalUserId = externalUserId,
                    Parsed = parsed,
                    UserId = parsed ? userId : Guid.Empty
                };
            })
            .ToArray();

        var guidUserIds = parsedUserIds
            .Where(x => x.Parsed)
            .Select(x => x.UserId)
            .Distinct()
            .ToArray();

        if (guidUserIds.Length == 0)
        {
            if (requireRegisteredDevices)
            {
                return Array.Empty<LocalizedRecipientBatch>();
            }

            return [new LocalizedRecipientBatch(null, externalUserIds.ToArray())];
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var activeDevicesQuery = dbContext.UserPushDevices
            .AsNoTracking()
            .Where(device => guidUserIds.Contains(device.UserId) && device.IsActive && device.NotificationsEnabled);

        var usersWithRegisteredDevices = await dbContext.UserPushDevices
            .AsNoTracking()
            .Where(device => guidUserIds.Contains(device.UserId) && device.IsActive)
            .Select(device => device.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var usersWithRegisteredDeviceSet = usersWithRegisteredDevices.ToHashSet();

        var devicesQuery = ApplyCategoryFilter(activeDevicesQuery, category);

        var deviceLocales = await devicesQuery
            .Select(device => new
            {
                device.UserId,
                device.Locale,
                device.LastSeenAtUtc,
                device.LastRegisteredAtUtc
            })
            .ToListAsync(cancellationToken);

        var preferredLocaleByUserId = deviceLocales
            .GroupBy(device => device.UserId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(device => device.LastSeenAtUtc)
                    .ThenByDescending(device => device.LastRegisteredAtUtc)
                    .Select(device => NormalizeLocale(device.Locale))
                    .FirstOrDefault(locale => locale is not null));

        var optedInUserIds = deviceLocales
            .Select(device => device.UserId)
            .Distinct()
            .ToHashSet();

        if (requireRegisteredDevices)
        {
            return parsedUserIds
                .Where(item => item.Parsed && optedInUserIds.Contains(item.UserId))
                .GroupBy(item =>
                    preferredLocaleByUserId.TryGetValue(item.UserId, out var locale)
                        ? locale
                        : null)
                .Select(group => new LocalizedRecipientBatch(
                    group.Key,
                    group.Select(item => item.ExternalUserId).ToArray()))
                .ToArray();
        }

        return parsedUserIds
            .Where(item =>
                !item.Parsed ||
                !usersWithRegisteredDeviceSet.Contains(item.UserId) ||
                optedInUserIds.Contains(item.UserId))
            .GroupBy(item =>
                item.Parsed && preferredLocaleByUserId.TryGetValue(item.UserId, out var locale)
                    ? locale
                    : null)
            .Select(group => new LocalizedRecipientBatch(
                group.Key,
                group.Select(item => item.ExternalUserId).ToArray()))
            .ToArray();
    }

    private async Task<string> ResolveNoRecipientsSkipReasonAsync(
        IReadOnlyCollection<string> lookupExternalUserIds,
        CancellationToken cancellationToken)
    {
        var guidUserIds = lookupExternalUserIds
            .Select(id => Guid.TryParse(id, out var userId) ? userId : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();

        if (guidUserIds.Length == 0)
        {
            return "No eligible OneSignal recipients were found.";
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var hasAnyActiveDevice = await dbContext.UserPushDevices
            .AsNoTracking()
            .AnyAsync(
                device => guidUserIds.Contains(device.UserId) && device.IsActive && device.NotificationsEnabled,
                cancellationToken);

        return hasAnyActiveDevice
            ? "Push notifications are disabled for this category on all registered devices."
            : "No registered push devices found.";
    }

    private static IQueryable<Domain.Modules.Identity.Entities.UserPushDevice> ApplyCategoryFilter(
        IQueryable<Domain.Modules.Identity.Entities.UserPushDevice> query,
        string? category)
    {
        var normalizedCategory = NormalizeCategory(category);

        return normalizedCategory switch
        {
            "dispatch" => query.Where(device => device.DispatchPushEnabled),
            "assignment" => query.Where(device => device.AssignmentPushEnabled),
            "support" => query.Where(device => device.SupportPushEnabled),
            "wallet" => query.Where(device => device.WalletPushEnabled),
            "account" => query.Where(device => device.AccountPushEnabled),
            _ => query
        };
    }

    private static string? NormalizeCategory(string? category) =>
        string.IsNullOrWhiteSpace(category) ? null : category.Trim().ToLowerInvariant();

    private static string? NormalizeLocale(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
        {
            return null;
        }

        var normalized = locale.Trim().Replace('_', '-');
        if (normalized.StartsWith("ar", StringComparison.OrdinalIgnoreCase))
        {
            return "ar";
        }

        if (normalized.StartsWith("en", StringComparison.OrdinalIgnoreCase))
        {
            return "en";
        }

        return null;
    }

    private static string? ResolvePreferredLocaleForBatch(
        string? deviceLocale,
        OneSignalApplicationTarget targetApplication) =>
        NormalizeLocale(deviceLocale) ?? (targetApplication == OneSignalApplicationTarget.AdminWeb ? "ar" : null);

    private (string AppId, string RestApiKey) ResolveAppConfiguration(
        OneSignalApplicationTarget targetApplication)
    {
        var customerAppId = ResolveSettingValue("OneSignal__AppId", _settings.AppId);
        var customerRestApiKey = ResolveSettingValue("OneSignal__RestApiKey", _settings.RestApiKey);

        if (targetApplication == OneSignalApplicationTarget.Driver)
        {
            return ResolveSeparateAppConfiguration(
                applicationName: "Driver",
                appIdEnvVar: "OneSignal__DriverAppId",
                appIdConfigured: _settings.DriverAppId,
                restApiKeyEnvVar: "OneSignal__DriverRestApiKey",
                restApiKeyConfigured: _settings.DriverRestApiKey,
                fallbackAppId: customerAppId,
                fallbackRestApiKey: customerRestApiKey);
        }

        if (targetApplication == OneSignalApplicationTarget.AdminWeb)
        {
            return ResolveSeparateAppConfiguration(
                applicationName: "AdminWeb",
                appIdEnvVar: "OneSignal__AdminWebAppId",
                appIdConfigured: _settings.AdminWebAppId,
                restApiKeyEnvVar: "OneSignal__AdminWebRestApiKey",
                restApiKeyConfigured: _settings.AdminWebRestApiKey,
                fallbackAppId: customerAppId,
                fallbackRestApiKey: customerRestApiKey);
        }

        return (customerAppId, customerRestApiKey);
    }

    /// <summary>
    /// Resolves OneSignal credentials for a target application that has its own
    /// dedicated OneSignal app (Driver or AdminWeb).
    /// Prevents the dangerous silent fallback that mixes a Customer key with a
    /// non-Customer App ID (which causes OneSignal to return 401 Access denied).
    /// Returns empty strings to mark configuration as not-ready when credentials
    /// are inconsistent, so dispatch is skipped with a clear, logged reason
    /// instead of being silently rejected by OneSignal.
    /// </summary>
    private (string AppId, string RestApiKey) ResolveSeparateAppConfiguration(
        string applicationName,
        string appIdEnvVar,
        string appIdConfigured,
        string restApiKeyEnvVar,
        string restApiKeyConfigured,
        string fallbackAppId,
        string fallbackRestApiKey)
    {
        var dedicatedAppId = ResolveSettingValue(appIdEnvVar, appIdConfigured);
        var dedicatedRestApiKey = ResolveSettingValue(restApiKeyEnvVar, restApiKeyConfigured);

        var hasDedicatedAppId = !string.IsNullOrWhiteSpace(dedicatedAppId);
        var hasDedicatedRestApiKey = !string.IsNullOrWhiteSpace(dedicatedRestApiKey);

        // Both dedicated values present: use the dedicated OneSignal app.
        if (hasDedicatedAppId && hasDedicatedRestApiKey)
        {
            return (dedicatedAppId, dedicatedRestApiKey);
        }

        // Both dedicated values missing: fall back to the Customer app entirely.
        // This is intentional and safe because both AppId and RestApiKey come from
        // the same Customer pair, so OneSignal authorization will succeed.
        if (!hasDedicatedAppId && !hasDedicatedRestApiKey)
        {
            return (fallbackAppId, fallbackRestApiKey);
        }

        // Inconsistent configuration: only one of (AppId, RestApiKey) is set.
        // Mixing a dedicated AppId with the Customer RestApiKey (or vice versa)
        // makes OneSignal reject the request with 401 Access denied, which silently
        // drops background/killed-state push delivery and is very hard to diagnose.
        // Fall back to the Customer pair as a whole if it is available, so mobile
        // push delivery does not stop because of a partially configured dedicated app.
        if (!string.IsNullOrWhiteSpace(fallbackAppId) && !string.IsNullOrWhiteSpace(fallbackRestApiKey))
        {
            _logger.LogWarning(
                "[PUSH-CONFIG] {ApplicationName} OneSignal credentials are inconsistent. " +
                "{AppIdEnvVar} set: {HasAppId}. {RestApiKeyEnvVar} set: {HasRestApiKey}. " +
                "Falling back to the Customer OneSignal app pair.",
                applicationName,
                appIdEnvVar,
                hasDedicatedAppId,
                restApiKeyEnvVar,
                hasDedicatedRestApiKey);

            return (fallbackAppId, fallbackRestApiKey);
        }

        _logger.LogError(
            "[PUSH-CONFIG] {ApplicationName} OneSignal credentials are inconsistent. " +
            "{AppIdEnvVar} set: {HasAppId}. {RestApiKeyEnvVar} set: {HasRestApiKey}. " +
            "Both must be configured together, and Customer fallback credentials are unavailable. " +
            "Skipping dispatch to avoid sending a request OneSignal will reject with 401.",
            applicationName,
            appIdEnvVar,
            hasDedicatedAppId,
            restApiKeyEnvVar,
            hasDedicatedRestApiKey);

        return (string.Empty, string.Empty);
    }

    private (string AppId, string RestApiKey) ResolveAppConfiguration(
        OneSignalPushProfile profile,
        string? category)
    {
        return ResolveAppConfiguration(ResolveTargetApplication(category));
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values
            .Select(NormalizeSettingValue)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private string ResolveSettingValue(string environmentVariableName, string configuredValue) =>
        _settings.UseEnvironmentVariableFallback
            ? FirstNonEmpty(
                configuredValue,
                Environment.GetEnvironmentVariable(environmentVariableName),
                Environment.GetEnvironmentVariable(environmentVariableName, EnvironmentVariableTarget.User),
                Environment.GetEnvironmentVariable(environmentVariableName, EnvironmentVariableTarget.Machine))
            : FirstNonEmpty(configuredValue);

    private static OneSignalApplicationTarget ResolveTargetApplication(string? category)
    {
        var normalizedCategory = NormalizeCategory(category);
        return normalizedCategory is "dispatch" or "assignment" or "support" or "wallet" or "account"
            ? OneSignalApplicationTarget.Driver
            : OneSignalApplicationTarget.Customer;
    }

    private static string BuildMissingConfigurationReason(OneSignalApplicationTarget targetApplication) =>
        targetApplication switch
        {
            OneSignalApplicationTarget.Driver =>
                "Driver OneSignal AppId or RestApiKey is not configured. Set OneSignal__DriverAppId and OneSignal__DriverRestApiKey, or configure OneSignal__AppId and OneSignal__RestApiKey as a fallback.",
            OneSignalApplicationTarget.AdminWeb =>
                "Admin web OneSignal AppId or RestApiKey is not configured. Set OneSignal__AdminWebAppId and OneSignal__AdminWebRestApiKey, or configure OneSignal__AppId and OneSignal__RestApiKey as a fallback.",
            _ =>
                "Customer OneSignal AppId or RestApiKey is not configured. Set OneSignal__AppId and OneSignal__RestApiKey."
        };

    private static string NormalizeSettingValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return IsPlaceholderValue(trimmed) ? string.Empty : trimmed;
    }

    private static bool IsPlaceholderValue(string value) =>
        value.Contains("__SET_", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("YOUR_", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("PUT_", StringComparison.OrdinalIgnoreCase) ||
        (value.StartsWith("<<", StringComparison.Ordinal) && value.EndsWith(">>", StringComparison.Ordinal));

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
                ApplyMobileProfile(
                    payload,
                    _settings.MobileHeadsUpExistingAndroidChannelId,
                    _settings.MobileHeadsUpAndroidChannelId,
                    _settings.MobileHeadsUpPriority);
                return;

            case OneSignalPushProfile.MobileStandard:
                ApplyMobileProfile(
                    payload,
                    _settings.MobileStandardExistingAndroidChannelId,
                    _settings.MobileStandardAndroidChannelId,
                    _settings.MobileStandardPriority);
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
        payload["android_visibility"] = 1;
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

    private string? ResolveTargetUrl(string? requestedTargetUrl, OneSignalApplicationTarget? targetApplication = null)
    {
        var configuredDefaultWebUrl = targetApplication == OneSignalApplicationTarget.AdminWeb
            ? FirstNonEmpty(
                ResolveSettingValue("OneSignal__AdminDefaultWebUrl", _settings.AdminDefaultWebUrl),
                ResolveSettingValue("OneSignal__DefaultWebUrl", _settings.DefaultWebUrl))
            : ResolveSettingValue("OneSignal__DefaultWebUrl", _settings.DefaultWebUrl);

        if (string.IsNullOrWhiteSpace(requestedTargetUrl))
        {
            return string.IsNullOrWhiteSpace(configuredDefaultWebUrl) ? null : configuredDefaultWebUrl;
        }

        if (Uri.TryCreate(requestedTargetUrl, UriKind.Absolute, out var absolute))
        {
            return absolute.ToString();
        }

        if (string.IsNullOrWhiteSpace(configuredDefaultWebUrl) ||
            !Uri.TryCreate(configuredDefaultWebUrl, UriKind.Absolute, out var baseUri))
        {
            // OneSignal web push requires an absolute URL; relative paths are ignored here.
            return null;
        }

        return new Uri(baseUri, requestedTargetUrl).ToString();
    }

    private static bool ShouldIncludeWebUrl(OneSignalPushProfile profile) =>
        profile == OneSignalPushProfile.Default;

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

    private static bool HasProviderRecipientErrors(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return false;
        }

        try
        {
            using var json = JsonDocument.Parse(responseBody);
            return json.RootElement.TryGetProperty("errors", out var errorsElement) &&
                   errorsElement.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined;
        }
        catch
        {
            return false;
        }
    }

    internal static bool HasSuccessfulNotificationId(string? responseBody) =>
        !string.IsNullOrWhiteSpace(ExtractNotificationId(responseBody));

    private static string ResolveChannel(Dictionary<string, object?> payload)
    {
        if (TryGetString(payload, "existing_android_channel_id", out var existingChannelId))
        {
            return $"existing_android_channel_id:{existingChannelId}";
        }

        if (TryGetString(payload, "android_channel_id", out var androidChannelId))
        {
            return $"android_channel_id:{androidChannelId}";
        }

        return "none";
    }

    private static string ResolveDataKeys(Dictionary<string, object?> payload)
    {
        if (!payload.TryGetValue("data", out var dataValue) ||
            dataValue is not Dictionary<string, object?> data)
        {
            return "none";
        }

        return string.Join(",", data.Keys.OrderBy(key => key, StringComparer.Ordinal));
    }

    private static bool TryGetString(
        IReadOnlyDictionary<string, object?> payload,
        string key,
        out string? value)
    {
        if (payload.TryGetValue(key, out var rawValue) &&
            rawValue is string stringValue &&
            !string.IsNullOrWhiteSpace(stringValue))
        {
            value = stringValue;
            return true;
        }

        value = null;
        return false;
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

    private sealed record PreparedOneSignalPayload(
        Dictionary<string, object?> Payload,
        string AppId,
        int ExternalUserCount,
        string ExternalIdBatch,
        string RestApiKey,
        OneSignalPushProfile Profile,
        Guid? ReferenceId,
        Guid NotificationEventId,
        string? Type,
        string? PreferredLocale,
        string Channel,
        string DataKeys);

    private sealed record LocalizedRecipientBatch(
        string? Locale,
        string[] ExternalUserIds);

    private sealed record ParsedExternalUserId(string RawId, Guid? ParsedId);

    private sealed record DriverPushIdentity(Guid UserId, Guid DriverId);

    private sealed record PushRecipientIdentity(
        string[] LookupExternalUserIds,
        IReadOnlyDictionary<string, string> PushExternalUserIdByLookupExternalUserId)
    {
        public static PushRecipientIdentity PassThrough(IReadOnlyCollection<string> externalUserIds) =>
            new(
                externalUserIds.ToArray(),
                externalUserIds.ToDictionary(id => id, id => id, StringComparer.Ordinal));

        public string ResolvePushExternalUserId(string lookupExternalUserId) =>
            PushExternalUserIdByLookupExternalUserId.TryGetValue(lookupExternalUserId, out var pushExternalUserId)
                ? pushExternalUserId
                : lookupExternalUserId;
    }

    private static string MaskSecretSuffix(string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            return "none";
        }

        var trimmed = secret.Trim();
        return trimmed.Length <= 6 ? trimmed : trimmed[^6..];
    }
}
