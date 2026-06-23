using System.Text.Json;

namespace Zadana.Domain.Modules.Social.Support;

public static class NotificationCategorySoundMap
{
    public const string DefaultCategory = "default";

    public static readonly string[] MobileCategories =
    [
        "dispatch",
        "assignment",
        "support",
        "wallet",
        "account"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IReadOnlyDictionary<string, string> Parse(string? json, string defaultSound)
    {
        var normalizedDefault = NotificationSoundCatalog.Normalize(defaultSound);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [DefaultCategory] = normalizedDefault
        };

        foreach (var category in MobileCategories)
        {
            result[category] = normalizedDefault;
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            return result;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions);
            if (parsed is null)
            {
                return result;
            }

            foreach (var (key, value) in parsed)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                var normalizedKey = key.Trim().ToLowerInvariant();
                if (!IsSupportedCategory(normalizedKey))
                {
                    continue;
                }

                result[normalizedKey] = NotificationSoundCatalog.Normalize(value, normalizedDefault);
            }
        }
        catch (JsonException)
        {
            return result;
        }

        return result;
    }

    public static string Serialize(IReadOnlyDictionary<string, string>? sounds, string defaultSound)
    {
        var normalized = BuildEffectiveMap(sounds, defaultSound);
        var payload = MobileCategories.ToDictionary(
            category => category,
            category => normalized[category],
            StringComparer.Ordinal);

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    public static IReadOnlyDictionary<string, string> BuildEffectiveMap(
        IReadOnlyDictionary<string, string>? sounds,
        string defaultSound)
    {
        var normalizedDefault = NotificationSoundCatalog.Normalize(defaultSound);
        var result = Parse(null, normalizedDefault);

        if (sounds is null || sounds.Count == 0)
        {
            return result;
        }

        var mutable = new Dictionary<string, string>(result, StringComparer.OrdinalIgnoreCase);

        if (sounds.TryGetValue(DefaultCategory, out var defaultOverride) &&
            !string.IsNullOrWhiteSpace(defaultOverride))
        {
            normalizedDefault = NotificationSoundCatalog.Normalize(defaultOverride, normalizedDefault);
            mutable[DefaultCategory] = normalizedDefault;
        }

        foreach (var category in MobileCategories)
        {
            mutable[category] = normalizedDefault;
        }

        foreach (var (key, value) in sounds)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var normalizedKey = key.Trim().ToLowerInvariant();
            if (!IsSupportedCategory(normalizedKey) || normalizedKey == DefaultCategory)
            {
                continue;
            }

            mutable[normalizedKey] = NotificationSoundCatalog.Normalize(value, normalizedDefault);
        }

        mutable[DefaultCategory] = normalizedDefault;
        return mutable;
    }

    public static string ResolveForCategory(
        string? categoryNotificationSoundsJson,
        string defaultSound,
        string? category)
    {
        var map = Parse(categoryNotificationSoundsJson, defaultSound);
        var normalizedCategory = string.IsNullOrWhiteSpace(category)
            ? null
            : category.Trim().ToLowerInvariant();

        if (normalizedCategory is not null &&
            map.TryGetValue(normalizedCategory, out var categorySound))
        {
            return categorySound;
        }

        return map.TryGetValue(DefaultCategory, out var fallback)
            ? fallback
            : NotificationSoundCatalog.Normalize(defaultSound);
    }

    private static bool IsSupportedCategory(string category) =>
        category == DefaultCategory || MobileCategories.Contains(category, StringComparer.Ordinal);
}
